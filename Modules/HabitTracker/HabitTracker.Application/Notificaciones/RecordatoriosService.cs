using HabitTracker.Application.DTOs;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Notificaciones;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Notificaciones;

/// <summary>Un botón del correo de recordatorio.</summary>
public sealed record AccionRecordatorio(string Texto, string Url);

/// <summary>El correo ya armado. Nunca lleva datos de salud: solo el nombre del plan y de sus tareas.</summary>
public sealed record CorreoRecordatorio(
    string Asunto, string Titulo, string Mensaje, IReadOnlyList<AccionRecordatorio> Acciones, string EnlaceBaja);

/// <summary>Por dónde salen los recordatorios (cuentas, preferencias y cola de correos). Lo implementa un adaptador del Host (RD-03).</summary>
public interface ICanalRecordatorios
{
    /// <summary>
    /// El correo de la persona si puede recibir recordatorios ahora: cuenta activa, sin baja programada y con los correos
    /// encendidos en Notificaciones. <c>null</c> si no.
    /// </summary>
    Task<string?> DestinatarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Deja el correo en la cola (no espera al servidor de correo).</summary>
    Task EncolarAsync(string destinatario, CorreoRecordatorio correo, CancellationToken cancellationToken = default);
}

/// <summary>Configuración de los recordatorios (sección <c>Recordatorios</c>, más las URL de <c>App</c>).</summary>
public sealed class OpcionesRecordatorios
{
    public bool Habilitado { get; set; } = true;
    public int IntervaloMinutos { get; set; } = 5;

    /// <summary>URL pública de la API (de ella cuelgan los enlaces «Ya lo hice» y «Darme de baja»).</summary>
    public string UrlApi { get; set; } = "";

    /// <summary>URL de la app (el botón «Abrir la app»).</summary>
    public string UrlApp { get; set; } = "";

    public void Validar()
    {
        if (IntervaloMinutos is < 1 or > 60)
            throw new InvalidOperationException("Recordatorios:IntervaloMinutos debe estar entre 1 y 60.");
        if (!EsHttp(UrlApi) || !EsHttp(UrlApp))
            throw new InvalidOperationException("Los recordatorios necesitan App:BaseUrl (y App:FrontendUrl, si la app vive aparte) como URL http(s).");
    }

    private static bool EsHttp(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

public interface IRecordatoriosService
{
    /// <summary>Una pasada del proceso: envía los recordatorios que tocan ahora. Devuelve cuántos encoló.</summary>
    Task<int> EjecutarAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// «Darme de baja» desde el correo: apaga el recordatorio de todos sus planes. Idempotente; un enlace no válido lanza
    /// <see cref="EnlaceNoValidoException"/>.
    /// </summary>
    Task DarDeBajaAsync(string token, CancellationToken cancellationToken = default);
}

/// <summary>
/// Recordatorios por correo de los planes (RF-NEG-07, Fase 5). Cada plan con el recordatorio encendido (lo enciende la persona:
/// opt-in) recibe como mucho un correo al día a su hora local, con un botón «Ya lo hice» por cada tarea de una vez que le
/// falte. La cadencia solo reduce (<see cref="DecisorRecordatorio"/>), y cada envío se reclama de forma atómica antes de encolarse.
/// </summary>
public sealed class RecordatoriosService(
    IPlanUsuarioRepository planes,
    IHabitoRepository habitos,
    IPerfilUsuarioRepository perfiles,
    IFirmadorEnlaces firmador,
    ICanalRecordatorios canal,
    OpcionesRecordatorios opciones,
    IUnitOfWork unitOfWork) : IRecordatoriosService
{
    /// <summary>Más botones no caben bien en un correo de móvil; el resto se hace desde la app.</summary>
    public const int MaximoBotones = 5;

    public async Task<int> EjecutarAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default)
    {
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(ahoraUtc));

        var enviados = 0;
        // shortcut: todos los planes con recordatorio en cada pasada; basta para el piloto (decenas). Con miles, filtrar por hora UTC.
        foreach (var dePersona in (await planes.ListarConRecordatorioAsync(cancellationToken)).GroupBy(p => p.UsuarioId))
        {
            enviados += await EnviarDePersonaAsync(dePersona.Key, [.. dePersona], ahoraUtc, cancellationToken);
        }

        return enviados;
    }

    private async Task<int> EnviarDePersonaAsync(Guid usuarioId, IReadOnlyList<PlanUsuario> suyos, DateTime ahoraUtc, CancellationToken cancellationToken)
    {
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        var zona = Seguimiento.Zona(perfil);
        var hoy = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zona));
        var todos = await habitos.GetByUsuarioIdSoloLecturaAsync(usuarioId, cancellationToken);

        // La actividad es el último registro: sin píxeles de seguimiento ni otra columna que mantener.
        var ultimo = todos.SelectMany(h => h.Registros).Select(r => (DateTime?)DateTime.SpecifyKind(r.Fecha, DateTimeKind.Utc)).Max();
        var diasSinActividad = CalculadoraCadencia.DiasSinActividad(ultimo, zona, ahoraUtc);

        var pendientes = new List<(PlanUsuario Plan, TipoRecordatorio Tipo, IReadOnlyList<TareaHoyDto> Tareas)>();
        foreach (var plan in suyos)
        {
            if (DecisorRecordatorio.Decidir(plan.HoraRecordatorio!.Value, zona, ahoraUtc, plan.UltimoRecordatorioLocal, diasSinActividad) is not { } tipo)
                continue;

            var jornada = perfil is null ? null : JornadaPersona.Desde(perfil);
            var tareas = Seguimiento.HabitosDe(plan, todos)
                .Where(h => h.Estado == EstadoHabito.Activo)
                .Select(h => HoyService.TareaDeHoy(plan, h, hoy, zona, jornada))
                .Where(t => t.Estado == EstadoTareaHoy.Pendiente)
                .ToList();
            // Con todo hecho no hay nada que recordar (el «¿seguimos?» sí sale: es justamente para quien no vuelve).
            if (tareas.Count == 0 && tipo != TipoRecordatorio.AvisoRetomar)
                continue;

            pendientes.Add((plan, tipo, tareas));
        }

        if (pendientes.Count == 0)
            return 0;

        // Se pregunta por la cuenta solo si hay algo que enviar: la mayoría de las pasadas no tocan Control de Acceso.
        if (await canal.DestinatarioAsync(usuarioId, cancellationToken) is not { } destinatario)
            return 0;

        var enviados = 0;
        foreach (var (plan, tipo, tareas) in pendientes)
        {
            // Primero se reclama: si otra pasada se adelantó, no sale un segundo correo. Si encolar falla después, ese día se
            // pierde (como mucho un correo; nunca dos).
            if (!await planes.ReclamarRecordatorioAsync(plan.Id, hoy, cancellationToken))
                continue;

            await canal.EncolarAsync(destinatario, Armar(plan, tipo, tareas, usuarioId, hoy), cancellationToken);
            enviados++;
        }

        return enviados;
    }

    private CorreoRecordatorio Armar(PlanUsuario plan, TipoRecordatorio tipo, IReadOnlyList<TareaHoyDto> tareas, Guid usuarioId, DateOnly hoy)
    {
        var api = opciones.UrlApi.TrimEnd('/');
        var acciones = tareas
            // «Ya lo hice» solo en las tareas de una vez (el check-in por correo registra 1); las demás, desde la app.
            .Where(t => t.CantidadObjetivo == 1m)
            .Take(MaximoBotones - 1)
            .Select(t => new AccionRecordatorio(
                $"Ya lo hice: {t.Nombre}",
                $"{api}/api/reminders/checkin?token={Uri.EscapeDataString(firmador.FirmarCheckin(new DatosEnlaceCheckin(usuarioId, t.HabitoId, hoy)))}"))
            .Append(new AccionRecordatorio("Abrir la app", $"{opciones.UrlApp.TrimEnd('/')}/hoy"))
            .ToList();

        var lista = string.Join("\n", tareas.Select(t => $"• {t.Nombre}"));
        var (asunto, mensaje) = tipo switch
        {
            TipoRecordatorio.AvisoRetomar => (
                $"¿Seguimos con «{plan.Nombre}»?",
                "Hace unas semanas que no registras nada, y está bien: a veces la vida se complica. No te enviaremos más " +
                "recordatorios de este plan hasta que vuelvas a marcar algo. Cuando quieras retomar, aquí estará."),
            TipoRecordatorio.Regreso => (
                $"Volver también cuenta: «{plan.Nombre}»",
                $"Hace unos días que no te vemos. Con lo mínimo de hoy basta para retomar:\n{lista}"),
            _ => (
                $"Tu plan de hoy: «{plan.Nombre}»",
                $"Esto es lo que te queda para hoy. Si ya lo hiciste, márcalo con un toque:\n{lista}"),
        };

        var baja = $"{api}/api/reminders/unsubscribe?token={Uri.EscapeDataString(firmador.FirmarBaja(usuarioId))}";
        return new CorreoRecordatorio(asunto, plan.Nombre, mensaje, acciones, baja);
    }

    public async Task DarDeBajaAsync(string token, CancellationToken cancellationToken = default)
    {
        var usuarioId = firmador.LeerBaja(token) ?? throw new EnlaceNoValidoException();

        foreach (var plan in await planes.ListarDeUsuarioAsync(usuarioId, cancellationToken))
        {
            if (plan.EstaEnCurso && plan.RecordatoriosActivos)
                plan.ConfigurarRecordatorio(null, false);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
