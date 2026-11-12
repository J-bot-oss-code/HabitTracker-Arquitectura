using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Motivacion;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Services;

public interface ILogrosService
{
    /// <summary>
    /// Gana y guarda los logros que correspondan a la conducta de la persona (idempotente: cada logro se obtiene una vez) y
    /// devuelve todos los suyos. Solo recibe el progreso y los hábitos: nunca peso ni medidas (P4).
    /// </summary>
    Task<IReadOnlyList<LogroUsuario>> SincronizarAsync(
        Guid usuarioId, IReadOnlyList<ProgresoDePlan> progresos, IReadOnlyList<Habito> habitos, CancellationToken cancellationToken = default);
}

public sealed class LogrosService(ILogroUsuarioRepository logros, TimeProvider? reloj = null) : ILogrosService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<IReadOnlyList<LogroUsuario>> SincronizarAsync(
        Guid usuarioId, IReadOnlyList<ProgresoDePlan> progresos, IReadOnlyList<Habito> habitos, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var existentes = (await logros.ListarAsync(usuarioId, cancellationToken)).ToList();

        var entrada = new EntradaLogros(
            AvancesRegistrados: habitos.Sum(h => h.Registros.Count),
            HabitosCompletados: habitos.Count(h => h.Estado == EstadoHabito.Completado),
            RachaMasLarga: progresos.Select(p => p.Progreso.RachaMasLarga).DefaultIfEmpty(0).Max(),
            HuboRegreso: progresos.Any(p => p.Progreso.Regresos.Count > 0),
            MitadDelPlanAlcanzada: progresos.Any(p => p.Plan.SemanasTotales > 0 && p.Progreso.DiasTranscurridos * 2 >= p.Plan.SemanasTotales * PlanUsuario.DiasPorSemana),
            PlanCompletado: progresos.Any(p => p.Plan.Estado == EstadoPlanUsuario.Completado));

        var nuevos = CalculadoraLogros.Evaluar(entrada, existentes.Select(l => l.Clave).ToHashSet());
        if (nuevos.Count == 0)
            return existentes;

        // Se guardan de inmediato y de forma idempotente: dos consultas a la vez (Hoy y el resumen) ganan los mismos logros y
        // la segunda no falla ni duplica. Se devuelve el estado real de la base, no el que se esperaba.
        var ahora = Guardas.Ahora(_reloj);
        foreach (var clave in nuevos)
            await logros.AgregarSiNoExisteAsync(new LogroUsuario(usuarioId, clave, ahora), cancellationToken);

        return [.. await logros.ListarAsync(usuarioId, cancellationToken)];
    }
}

public interface IHoyService
{
    /// <summary>
    /// Lo que la persona hace hoy: las tareas de sus planes activos con su estado, el mensaje del día y, si corresponde, la
    /// celebración de la semana. Es otro endpoint que <c>plan-del-dia</c> (clima): no depende de ningún servicio externo.
    /// </summary>
    Task<HoyDto> ObtenerHoyAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed class HoyService(
    IPlanUsuarioRepository planes,
    IPlanPlantillaRepository plantillas,
    IHabitoRepository habitos,
    IPerfilUsuarioRepository perfiles,
    ILogrosService logros,
    ICatalogoMensajes mensajes,
    TimeProvider? reloj = null) : IHoyService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<HoyDto> ObtenerHoyAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var ahora = Guardas.Ahora(_reloj);
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        var zona = Seguimiento.Zona(perfil);
        var hoy = CalculadoraProgreso.DiaLocal(zona, ahora);
        var todos = await habitos.GetByUsuarioIdSoloLecturaAsync(usuarioId, cancellationToken);
        var lista = await planes.ListarDeUsuarioAsync(usuarioId, cancellationToken);

        var progresos = lista
            .Select(p => { var h = Seguimiento.HabitosDe(p, todos); return new ProgresoDePlan(p, h, Seguimiento.Progreso(p, h, zona, ahora)); })
            .ToList();
        var activos = progresos.Where(p => p.Plan.Estado == EstadoPlanUsuario.Activo).OrderBy(p => p.Plan.FechaInicio).ToList();

        var jornada = perfil is null ? null : JornadaPersona.Desde(perfil);
        var tareas = activos
            .SelectMany(p => p.Habitos.Where(h => h.Estado == EstadoHabito.Activo).Select(h => TareaDeHoy(p.Plan, h, hoy, zona, jornada)))
            .ToList();

        var sincronizados = await logros.SincronizarAsync(usuarioId, progresos, todos, cancellationToken);
        var (mensaje, celebracion, tip) = await MensajeYCelebracionAsync(activos.FirstOrDefault(), usuarioId, hoy, ahora, cancellationToken);

        return new(
            hoy,
            tareas,
            [.. activos.Select(p => PlanDeHoy(p, ahora))],
            mensaje,
            celebracion,
            tip,
            [.. sincronizados.Where(l => l.VistoEn is null).Select(l => l.Clave)]);
    }

    private async Task<(string? Mensaje, int? Celebracion, string? Tip)> MensajeYCelebracionAsync(
        ProgresoDePlan? principal, Guid usuarioId, DateOnly hoy, DateTime ahora, CancellationToken cancellationToken)
    {
        if (principal is null)
            return (null, null, null);

        // Un plan activo que ya llegó a su fin espera a que la persona lo complete: el tono es de cierre.
        var categoria = SelectorMensajesMotivacion.Categorizar(SituacionMensaje.Desde(principal.Progreso, principal.Plan.LlegoFechaFin(ahora)));
        var mensaje = SelectorMensajesMotivacion.Elegir(mensajes.Todos, categoria, usuarioId, hoy);

        // La semana recién cerrada, si se celebró, es la anterior a la actual: ahí se muestra su tip extra una sola semana.
        var semanaCerrada = principal.Progreso.SemanaActual - 1;
        if (semanaCerrada < 1 || !principal.Progreso.SemanasCelebradas.Contains(semanaCerrada))
            return (mensaje, null, null);

        string? tip = null;
        if (principal.Plan.Slug is { } slug)
            tip = (await plantillas.ObtenerPorSlugsAsync([slug], cancellationToken)).FirstOrDefault()?.GuiaSemanal
                .FirstOrDefault(g => g.Semana == semanaCerrada)?.TipExtra;
        return (mensaje, semanaCerrada, tip);
    }

    private static PlanHoyDto PlanDeHoy(ProgresoDePlan p, DateTime ahoraUtc)
    {
        var s = SugeridorAjuste.Sugerir(p.Progreso, p.Plan.Estado, p.Plan.Extensiones, p.Plan.EnModoSuave(ahoraUtc));
        return new PlanHoyDto(p.Plan.Id, p.Plan.Nombre, p.Progreso.SemanaActual, p.Progreso.SemanasTotales, p.Progreso.DiasRestantes,
            p.Progreso.Racha, s is null ? null : new SugerenciaAjusteDto(s.Opciones, s.DiasSinRegistro));
    }

    private static TareaHoyDto TareaDeHoy(PlanUsuario plan, Habito habito, DateOnly hoy, TimeZoneInfo zona, JornadaPersona? jornada)
    {
        // El periodo de una tarea diaria es el día local; el de una semanal, la semana (lunes a domingo) local.
        var desde = habito.Periodicidad == Periodicidad.Semanal ? InicioDeSemana(hoy) : hoy;
        var delPeriodo = habito.Registros.Where(r => EnPeriodo(CalculadoraProgreso.DiaLocal(zona, r.Fecha), desde, hoy)).ToList();
        var cantidad = delPeriodo.Sum(r => r.Cantidad);

        var estado = cantidad >= habito.CantidadObjetivo ? EstadoTareaHoy.Hecho
            : delPeriodo.Count > 0 && delPeriodo.All(r => r.EsVersionMinima) ? EstadoTareaHoy.Minimo
            : EstadoTareaHoy.Pendiente;

        return new TareaHoyDto(
            habito.Id, plan.Id, plan.Nombre, habito.Nombre, habito.Descripcion, habito.Periodicidad, habito.CantidadObjetivo,
            habito.Unidad, habito.Ancla, Seguimiento.Hora(habito.Ancla, jornada), habito.VersionMinima, estado, cantidad,
            Math.Max(0m, habito.CantidadObjetivo - cantidad));
    }

    private static bool EnPeriodo(DateOnly dia, DateOnly desde, DateOnly hasta) => dia >= desde && dia <= hasta;

    private static DateOnly InicioDeSemana(DateOnly dia) =>
        dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7)); // lunes
}

public interface IResumenProgresoService
{
    /// <summary>Racha, récord, logros, "tu mes en números" y el mensaje del día. Gana los logros pendientes al consultarlo.</summary>
    Task<ResumenProgresoDto> ObtenerResumenAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Marca como vistos los logros celebrados: cada uno se felicita una sola vez.</summary>
    Task MarcarLogrosVistosAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed class ResumenProgresoService(
    IPlanUsuarioRepository planes,
    IHabitoRepository habitos,
    IPerfilUsuarioRepository perfiles,
    ILogrosService logrosServicio,
    ILogroUsuarioRepository logroRepositorio,
    ICatalogoMensajes mensajes,
    IUnitOfWork unitOfWork,
    TimeProvider? reloj = null) : IResumenProgresoService
{
    private const int DiasDelMesParaMantener = 3;
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<ResumenProgresoDto> ObtenerResumenAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var ahora = Guardas.Ahora(_reloj);
        var zona = Seguimiento.Zona(await perfiles.ObtenerAsync(usuarioId, cancellationToken));
        var hoy = CalculadoraProgreso.DiaLocal(zona, ahora);
        var todos = await habitos.GetByUsuarioIdSoloLecturaAsync(usuarioId, cancellationToken);

        var progresos = (await planes.ListarDeUsuarioAsync(usuarioId, cancellationToken))
            .Select(p => { var h = Seguimiento.HabitosDe(p, todos); return new ProgresoDePlan(p, h, Seguimiento.Progreso(p, h, zona, ahora)); })
            .ToList();
        var enCurso = progresos.Where(p => p.Plan.EstaEnCurso).ToList();
        var logros = await logrosServicio.SincronizarAsync(usuarioId, progresos, todos, cancellationToken);

        var principal = enCurso.Where(p => p.Plan.Estado == EstadoPlanUsuario.Activo).OrderBy(p => p.Plan.FechaInicio).FirstOrDefault();
        var mensaje = principal is null
            ? null
            : SelectorMensajesMotivacion.Elegir(
                mensajes.Todos,
                SelectorMensajesMotivacion.Categorizar(SituacionMensaje.Desde(principal.Progreso, principal.Plan.LlegoFechaFin(ahora))),
                usuarioId, hoy);

        return new ResumenProgresoDto(
            enCurso.Select(p => p.Progreso.Racha).DefaultIfEmpty(0).Max(),
            progresos.Select(p => p.Progreso.RachaMasLarga).DefaultIfEmpty(0).Max(),
            [.. logros.OrderBy(l => l.ObtenidoEn).Select(l => new LogroDto(l.Clave, l.ObtenidoEn, l.VistoEn is not null))],
            MesEnNumeros(progresos, todos, zona, hoy),
            mensaje,
            enCurso.Count);
    }

    public async Task MarcarLogrosVistosAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var ahora = Guardas.Ahora(_reloj);
        foreach (var logro in await logroRepositorio.ListarAsync(usuarioId, cancellationToken))
            logro.MarcarVisto(ahora);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // El mes local en curso, solo con los hábitos de los planes: días con avance, hábitos que se mantienen y regresos.
    private static MesEnNumerosDto MesEnNumeros(
        IReadOnlyList<ProgresoDePlan> progresos, IReadOnlyList<Habito> todos, TimeZoneInfo zona, DateOnly hoy)
    {
        bool EsDelMes(DateOnly d) => d.Year == hoy.Year && d.Month == hoy.Month;
        var deLosPlanes = todos.Where(h => h.PlanUsuarioId is not null).ToList();

        var diasPorHabito = deLosPlanes.ToDictionary(
            h => h.Id,
            h => h.Registros.Select(r => CalculadoraProgreso.DiaLocal(zona, r.Fecha)).Where(EsDelMes).ToHashSet());

        return new MesEnNumerosDto(
            diasPorHabito.Values.SelectMany(d => d).Distinct().Count(),
            deLosPlanes.Count(h => h.Estado == EstadoHabito.Activo && diasPorHabito[h.Id].Count >= DiasDelMesParaMantener),
            progresos.Sum(p => p.Progreso.Regresos.Count(EsDelMes)));
    }
}

public interface IDatosPlanesService
{
    Task<DatosPlanesDto> ExportarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Borra los planes, sus pausas y los logros (baja de cuenta). Inmediato e idempotente.</summary>
    Task EliminarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed class DatosPlanesService(IPlanUsuarioRepository planes, ILogroUsuarioRepository logros) : IDatosPlanesService
{
    public async Task<DatosPlanesDto> ExportarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var lista = await planes.ListarDeUsuarioAsync(usuarioId, cancellationToken);
        var propios = await logros.ListarAsync(usuarioId, cancellationToken);

        return new DatosPlanesDto(
            [.. lista.Select(p => new PlanExportadoDto(
                p.Id, p.Slug, p.Nombre, p.Estado, p.FechaInicio, p.FechaFinPrevista, p.Extensiones, p.ModoSuaveHasta,
                p.HoraRecordatorio?.ToString("HH:mm"), p.RecordatoriosActivos,
                [.. p.Pausas.Select(x => new PausaExportadaDto(x.InicioUtc, x.FinUtc, x.Motivo))]))],
            [.. propios.Select(l => new LogroDto(l.Clave, l.ObtenidoEn, l.VistoEn is not null))]);
    }

    public async Task EliminarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        await planes.EliminarDeUsuarioAsync(usuarioId, cancellationToken);
        await logros.EliminarDeUsuarioAsync(usuarioId, cancellationToken);
    }
}
