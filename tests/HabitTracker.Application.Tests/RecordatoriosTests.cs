using System.Security.Cryptography;
using System.Text;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Notificaciones;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Tests;

/// <summary>Fase 5: los recordatorios por correo de los planes y la baja en un clic (dobles escritos a mano, RD-12).</summary>
public class RecordatoriosTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    // Viernes 13-nov-2026, 8:30 locales en Santo Domingo (UTC-4). El plan pide su recordatorio a las 8:00.
    private static readonly DateTime Ahora = new(2026, 11, 13, 12, 30, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 11, 13);
    private static readonly TimeOnly Ocho = new(8, 0);

    // ---------- Dobles ----------

    private sealed class CifradorFalso : ICifradorDatos
    {
        public string Cifrar(string texto, string proposito) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"{proposito}|{texto}"));

        public string Descifrar(string cifrado, string proposito)
        {
            string plano;
            try { plano = Encoding.UTF8.GetString(Convert.FromBase64String(cifrado)); }
            catch (FormatException) { throw new CryptographicException("Token inválido."); }
            var prefijo = proposito + "|";
            return plano.StartsWith(prefijo, StringComparison.Ordinal) ? plano[prefijo.Length..] : throw new CryptographicException("Otro propósito.");
        }
    }

    private sealed class RelojFijo(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utc, TimeSpan.Zero);
    }

    /// <summary>Guarda los planes y reclama como la base: solo el primero que marca el día gana.</summary>
    private sealed class Planes : IPlanUsuarioRepository
    {
        public List<PlanUsuario> Lista { get; } = [];
        public Dictionary<Guid, DateOnly> Reclamados { get; } = [];

        public Task<PlanUsuario?> ObtenerAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Lista.FirstOrDefault(p => p.Id == planId && p.UsuarioId == usuarioId));
        public Task<IReadOnlyList<PlanUsuario>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanUsuario>>([.. Lista.Where(p => p.UsuarioId == usuarioId)]);
        public Task AgregarAsync(PlanUsuario plan, CancellationToken cancellationToken = default) { Lista.Add(plan); return Task.CompletedTask; }
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<PlanUsuario>> ListarConRecordatorioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanUsuario>>(
                [.. Lista.Where(p => p.Estado == EstadoPlanUsuario.Activo && p.RecordatoriosActivos && p.HoraRecordatorio != null)]);
        public Task<bool> ReclamarRecordatorioAsync(Guid planId, DateOnly hoyLocal, CancellationToken cancellationToken = default)
        {
            if (Reclamados.TryGetValue(planId, out var dia) && dia >= hoyLocal)
                return Task.FromResult(false);
            Reclamados[planId] = hoyLocal;
            return Task.FromResult(true);
        }
    }

    private sealed class Habitos : IHabitoRepository
    {
        public List<Habito> Lista { get; } = [];
        public Task<Habito?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(Lista.FirstOrDefault(h => h.Id == id));
        public Task<IReadOnlyList<Habito>> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Habito>>([.. Lista.Where(h => h.UsuarioId == usuarioId)]);
        public Task<IReadOnlyList<Habito>> GetByPlanUsuarioIdAsync(Guid usuarioId, Guid planUsuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Habito>>([.. Lista.Where(h => h.UsuarioId == usuarioId && h.PlanUsuarioId == planUsuarioId)]);
        public Task<IReadOnlyList<Habito>> GetByUsuarioIdSoloLecturaAsync(Guid usuarioId, CancellationToken cancellationToken = default) => GetByUsuarioIdAsync(usuarioId, cancellationToken);
        public Task AddAsync(Habito habito, CancellationToken cancellationToken = default) { Lista.Add(habito); return Task.CompletedTask; }
        public Task UpdateAsync(Habito habito, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(0);
    }

    private sealed class Perfiles : IPerfilUsuarioRepository
    {
        private readonly PerfilUsuario _perfil = PerfilUsuario.Crear(Usuario, new DatosPerfil(PerfilLaboral.OficinaDiurna, "America/Santo_Domingo", true), Ahora);
        public Task<PerfilUsuario?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(usuarioId == Usuario ? _perfil : null);
        public Task AgregarAsync(PerfilUsuario perfil, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Canal : ICanalRecordatorios
    {
        public string? Correo { get; set; } = "ana@ejemplo.com";
        public List<(string Para, CorreoRecordatorio Correo)> Enviados { get; } = [];
        public int Consultas { get; private set; }
        public Task<string?> DestinatarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) { Consultas++; return Task.FromResult(Correo); }
        public Task EncolarAsync(string destinatario, CorreoRecordatorio correo, CancellationToken cancellationToken = default)
        {
            Enviados.Add((destinatario, correo));
            return Task.CompletedTask;
        }
    }

    private sealed class UnidadDeTrabajo : IUnitOfWork
    {
        public int Guardados { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { Guardados++; return Task.FromResult(1); }
        public void Dispose() { }
    }

    private static PlanPlantilla Plantilla() =>
        new(new ContenidoPlan(
            "plan", "Caminar", "Resumen.", "Descripción.", AreaPlan.Estres, NivelPlan.Basico, IntensidadPlan.Baja, SexoPlan.Todos, 16, 100, 3, [], null,
            false, [], [], [], ["Uno", "Dos", "Tres"], "Aviso.", [], false, 1,
            [new TareaPlantilla(1, "Caminar 20 minutos", "Instrucciones.", Periodicidad.Diaria, 1, "veces", "Cinco minutos.", Ancla.AMediaTarde),
             new TareaPlantilla(2, "Beber agua", "Instrucciones.", Periodicidad.Diaria, 6, "vasos", "Un vaso.", Ancla.AMediaTarde)]));

    private sealed class Escenario
    {
        public Planes Planes { get; } = new();
        public Habitos Habitos { get; } = new();
        public Canal Canal { get; } = new();
        public UnidadDeTrabajo Unidad { get; } = new();
        public IFirmadorEnlaces Firmador { get; } = new FirmadorEnlaces(new CifradorFalso(), new RelojFijo(Ahora));
        public RecordatoriosService Servicio { get; }
        public PlanUsuario Plan { get; }

        public Escenario(bool recordatorio = true)
        {
            var plantilla = Plantilla();
            Plan = PlanUsuario.Iniciar(Usuario, plantilla, Ocho, Ahora.AddDays(-2));
            if (recordatorio)
                Plan.ConfigurarRecordatorio(null, true);
            Planes.Lista.Add(Plan);
            foreach (var tarea in plantilla.Tareas)
                Habitos.Lista.Add(Habito.DesdeTareaDePlan(Usuario, Plan.Id, tarea));
            Servicio = new RecordatoriosService(
                Planes, Habitos, new Perfiles(), Firmador, Canal,
                new OpcionesRecordatorios { UrlApi = "https://api.ejemplo.com", UrlApp = "https://app.ejemplo.com" }, Unidad);
        }

        public Habito Caminar => Habitos.Lista.Single(h => h.Nombre == "Caminar 20 minutos");
    }

    // ---------- Envío ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_ALaHoraElegida_SaleUnCorreoConLasTareasPendientesYSusBotones()
    {
        var e = new Escenario();

        Assert.Equal(1, await e.Servicio.EjecutarAsync(Ahora));

        var (para, correo) = Assert.Single(e.Canal.Enviados);
        Assert.Equal("ana@ejemplo.com", para);
        Assert.Contains("Caminar", correo.Asunto);
        Assert.Contains("Caminar 20 minutos", correo.Mensaje);
        Assert.Contains("Beber agua", correo.Mensaje);
        // «Ya lo hice» solo en la tarea de una vez (la de 6 vasos se registra en la app), y siempre «Abrir la app».
        Assert.Equal(["Ya lo hice: Caminar 20 minutos", "Abrir la app"], correo.Acciones.Select(a => a.Texto));
        Assert.Equal("https://app.ejemplo.com/hoy", correo.Acciones[^1].Url);
        Assert.StartsWith("https://api.ejemplo.com/api/reminders/unsubscribe?token=", correo.EnlaceBaja);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_07_ElBotonYaLoHice_LlevaUnEnlaceFirmadoDeEsaPersonaHabitoYDia()
    {
        var e = new Escenario();
        await e.Servicio.EjecutarAsync(Ahora);

        var url = e.Canal.Enviados.Single().Correo.Acciones[0].Url;
        Assert.StartsWith("https://api.ejemplo.com/api/reminders/checkin?token=", url);
        var token = Uri.UnescapeDataString(url[(url.IndexOf("token=", StringComparison.Ordinal) + 6)..]);
        Assert.Equal(new DatosEnlaceCheckin(Usuario, e.Caminar.Id, Hoy), e.Firmador.LeerCheckin(token));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_SinElRecordatorioEncendido_NoSaleNada_EsOptIn()
    {
        var e = new Escenario(recordatorio: false);

        Assert.Equal(0, await e.Servicio.EjecutarAsync(Ahora));
        Assert.Empty(e.Canal.Enviados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_UnCorreoPorDia_DosPasadasNoMandanDos()
    {
        var e = new Escenario();

        await e.Servicio.EjecutarAsync(Ahora);
        await e.Servicio.EjecutarAsync(Ahora.AddMinutes(5));

        Assert.Single(e.Canal.Enviados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_DosPasadasALaVez_ElReclamoAtomicoDejaUnSoloCorreo()
    {
        var e = new Escenario();

        // Las dos leen los planes antes de que ninguna marque el día: gana solo la que reclama primero.
        await Task.WhenAll(e.Servicio.EjecutarAsync(Ahora), e.Servicio.EjecutarAsync(Ahora));

        Assert.Single(e.Canal.Enviados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_ConTodoHechoHoy_NoHayNadaQueRecordar()
    {
        var e = new Escenario();
        foreach (var h in e.Habitos.Lista)
            h.RegistrarAvance(Usuario, h.CantidadObjetivo, Ahora.AddMinutes(-20));

        Assert.Equal(0, await e.Servicio.EjecutarAsync(Ahora));
        // Ni siquiera se pregunta por la cuenta: no había nada que enviar.
        Assert.Equal(0, e.Canal.Consultas);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_SoloLoQueFalta_UnaTareaHechaNoApareceEnElCorreo()
    {
        var e = new Escenario();
        e.Caminar.RegistrarAvance(Usuario, 1, Ahora.AddMinutes(-20));

        await e.Servicio.EjecutarAsync(Ahora);

        var correo = e.Canal.Enviados.Single().Correo;
        Assert.DoesNotContain("Caminar 20 minutos", correo.Mensaje);
        Assert.Equal(["Abrir la app"], correo.Acciones.Select(a => a.Texto));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_SiLaCuentaNoPuedeRecibir_NoSeEncolaNiSeGastaElDia()
    {
        // Cuenta inactiva, en baja o con los correos apagados en Notificaciones: el adaptador devuelve null.
        var e = new Escenario();
        e.Canal.Correo = null;

        Assert.Equal(0, await e.Servicio.EjecutarAsync(Ahora));
        Assert.Empty(e.Canal.Enviados);
        Assert.Empty(e.Planes.Reclamados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_UnPlanPausado_NoRecibeRecordatorios()
    {
        var e = new Escenario();
        e.Plan.Pausar(MotivoPausa.Viaje, Ahora.AddHours(-1));

        Assert.Equal(0, await e.Servicio.EjecutarAsync(Ahora));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_A_Los21DiasSinActividad_SaleUnSoloSeguimosYDespuesNada()
    {
        var e = new Escenario();
        e.Caminar.RegistrarAvance(Usuario, 1, Ahora.AddDays(-21));

        await e.Servicio.EjecutarAsync(Ahora);
        await e.Servicio.EjecutarAsync(Ahora.AddDays(1));
        await e.Servicio.EjecutarAsync(Ahora.AddDays(7));

        var (_, correo) = Assert.Single(e.Canal.Enviados);
        Assert.Contains("¿Seguimos", correo.Asunto);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-08")]
    public async Task RF_NEG_07_ElCorreoNuncaLlevaDatosDeSalud()
    {
        var e = new Escenario();

        await e.Servicio.EjecutarAsync(Ahora);

        var correo = e.Canal.Enviados.Single().Correo;
        var texto = string.Join(" ", correo.Asunto, correo.Titulo, correo.Mensaje, string.Join(" ", correo.Acciones.Select(a => a.Texto)));
        foreach (var palabra in new[] { "peso", "kg", "IMC", "calor", "medida", "salud", "condición" })
            Assert.DoesNotContain(palabra, texto, StringComparison.OrdinalIgnoreCase);
    }

    // ---------- Baja en un clic ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_ElEnlaceDeBaja_ApagaLosRecordatoriosYEsIdempotente()
    {
        var e = new Escenario();
        var token = e.Firmador.FirmarBaja(Usuario);

        await e.Servicio.DarDeBajaAsync(token);
        await e.Servicio.DarDeBajaAsync(token);

        Assert.False(e.Plan.RecordatoriosActivos);
        Assert.Equal(0, await e.Servicio.EjecutarAsync(Ahora));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-token")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_UnEnlaceDeBajaNoValido_SeRechazaSinTocarNada(string token)
    {
        var e = new Escenario();

        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.DarDeBajaAsync(token));

        Assert.True(e.Plan.RecordatoriosActivos);
        Assert.Equal(0, e.Unidad.Guardados);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_UnEnlaceDeCheckinNoSirveComoEnlaceDeBaja()
    {
        var e = new Escenario();
        var checkin = e.Firmador.FirmarCheckin(new DatosEnlaceCheckin(Usuario, e.Caminar.Id, Hoy));

        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.DarDeBajaAsync(checkin));
        Assert.True(e.Plan.RecordatoriosActivos);
    }
}
