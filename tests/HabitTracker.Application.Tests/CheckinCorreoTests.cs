using System.Security.Cryptography;
using System.Text;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Notificaciones;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Tests;

/// <summary>Fase 5: el enlace «Ya lo hice» del correo (token firmado y servicio de check-in, dobles escritos a mano, RD-12).</summary>
public class CheckinCorreoTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private static readonly Guid Otro = Guid.NewGuid();
    // Viernes 13-nov-2026, 10:00 locales en Santo Domingo (UTC-4).
    private static readonly DateTime Viernes = new(2026, 11, 13, 14, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Hoy = new(2026, 11, 13);

    // ---------- Dobles ----------

    private sealed class RelojFijo(DateTime utc) : TimeProvider
    {
        public DateTime Ahora { get; set; } = utc;
        public override DateTimeOffset GetUtcNow() => new(Ahora, TimeSpan.Zero);
    }

    /// <summary>Cifra de verdad lo que importa para los tests: un token solo se lee con el mismo propósito y sin alterarlo.</summary>
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

    private sealed class Directorio : IEstadoCuentas
    {
        public HashSet<Guid> Activos { get; } = [Usuario];
        public Task<bool> CuentaActivaAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(Activos.Contains(usuarioId));
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

    private sealed class Planes : IPlanUsuarioRepository
    {
        public List<PlanUsuario> Lista { get; } = [];
        public Task<PlanUsuario?> ObtenerAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Lista.FirstOrDefault(p => p.Id == planId && p.UsuarioId == usuarioId));
        public Task<IReadOnlyList<PlanUsuario>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanUsuario>>([.. Lista.Where(p => p.UsuarioId == usuarioId)]);
        public Task AgregarAsync(PlanUsuario plan, CancellationToken cancellationToken = default) { Lista.Add(plan); return Task.CompletedTask; }
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<IReadOnlyList<PlanUsuario>> ListarConRecordatorioAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PlanUsuario>>([]);
        public Task<bool> ReclamarRecordatorioAsync(Guid planId, DateOnly hoyLocal, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class Perfiles : IPerfilUsuarioRepository
    {
        private readonly PerfilUsuario _perfil = PerfilUsuario.Crear(Usuario, new DatosPerfil(PerfilLaboral.OficinaDiurna, "America/Santo_Domingo", true), Viernes);
        public Task<PerfilUsuario?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default) => Task.FromResult(usuarioId == Usuario ? _perfil : null);
        public Task AgregarAsync(PerfilUsuario perfil, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class UnidadDeTrabajo : IUnitOfWork
    {
        public int Guardados { get; private set; }
        public bool Conflicto { get; set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (Conflicto) throw new ConflictoDeUnicidadException();
            Guardados++;
            return Task.FromResult(1);
        }
        public void Dispose() { }
    }

    private sealed class Metas : IMetaRepository
    {
        public Task<bool> ExistsAsync(Guid metaId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class SinPermisos : IPermisoService
    {
        public Task<bool> TienePermisoEfectivoAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    private static PlanPlantilla Plantilla(decimal cantidad = 1) =>
        new(new ContenidoPlan(
            "plan", "Plan", "Resumen.", "Descripción.", AreaPlan.Estres, NivelPlan.Basico, IntensidadPlan.Baja, SexoPlan.Todos, 16, 100, 3, [], null,
            false, [], [], [], ["Uno", "Dos", "Tres"], "Aviso.", [], false, 1,
            [new TareaPlantilla(1, "Respirar", "Instrucciones.", Periodicidad.Diaria, cantidad, "veces", "Versión mínima.", Ancla.AMediaTarde)]));

    private sealed class Escenario
    {
        public RelojFijo Reloj { get; } = new(Viernes);
        public Directorio Directorio { get; } = new();
        public Habitos Habitos { get; } = new();
        public Planes Planes { get; } = new();
        public UnidadDeTrabajo Unidad { get; } = new();
        public IFirmadorEnlaces Firmador { get; }
        public CheckinCorreoService Servicio { get; }
        public PlanUsuario Plan { get; }
        public Habito Habito { get; }

        public Escenario(decimal cantidad = 1)
        {
            Firmador = new FirmadorEnlaces(new CifradorFalso(), Reloj);
            var plantilla = Plantilla(cantidad);
            Plan = PlanUsuario.Iniciar(Usuario, plantilla, null, Viernes);
            Planes.Lista.Add(Plan);
            Habito = Habito.DesdeTareaDePlan(Usuario, Plan.Id, plantilla.Tareas[0]);
            Habitos.Lista.Add(Habito);
            var habitos = new HabitoService(Habitos, Unidad, new SinPermisos(), new Metas(), Reloj);
            Servicio = new CheckinCorreoService(Firmador, Directorio, Habitos, Planes, new Perfiles(), habitos, Reloj);
        }

        public string Token(Guid? usuario = null, Guid? habito = null, DateOnly? fecha = null) =>
            Firmador.FirmarCheckin(new DatosEnlaceCheckin(usuario ?? Usuario, habito ?? Habito.Id, fecha ?? Hoy));
    }

    // ---------- El token ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_Token_IdaYVuelta_ConservaUsuarioHabitoYDia()
    {
        var e = new Escenario();

        var datos = e.Firmador.LeerCheckin(e.Token());

        Assert.Equal(new DatosEnlaceCheckin(Usuario, e.Habito.Id, Hoy), datos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_07_Token_Caduca_A_Las36Horas_Exactas()
    {
        var e = new Escenario();
        var token = e.Token();

        e.Reloj.Ahora = Viernes.AddHours(35).AddMinutes(59);
        Assert.NotNull(e.Firmador.LeerCheckin(token));

        e.Reloj.Ahora = Viernes.AddHours(36);
        Assert.Null(e.Firmador.LeerCheckin(token));
        e.Reloj.Ahora = Viernes.AddHours(36).AddMinutes(1);
        Assert.Null(e.Firmador.LeerCheckin(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-token")]
    [InlineData("AAAA")]
    [Trait("Requirement", "RD-07")]
    [Trait("Requirement", "RD-08")]
    public void RD_07_Token_BasuraONulo_SeLeeComoNull_SinLanzar(string token)
    {
        Assert.Null(new Escenario().Firmador.LeerCheckin(token));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Token_DemasiadoLargo_SeRechazaSinDescifrarlo()
    {
        Assert.Null(new Escenario().Firmador.LeerCheckin(new string('A', 2049)));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_Token_DeOtroProposito_NoSirve()
    {
        var e = new Escenario();
        var ajeno = new CifradorFalso().Cifrar("{\"U\":\"" + Usuario + "\"}", "Cuestionario.Objetivos.v1");

        Assert.Null(e.Firmador.LeerCheckin(ajeno));
    }

    // ---------- El servicio ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Ver_MuestraElNombreSinRegistrarNada()
    {
        var e = new Escenario();

        var info = await e.Servicio.VerAsync(e.Token());

        Assert.Equal(new InfoCheckin("Respirar", Hoy), info);
        Assert.Empty(e.Habito.Registros);
        Assert.Equal(0, e.Unidad.Guardados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Ejecutar_RegistraUnAvanceDeCantidad1_ConOrigenCorreoYClaveDelDia()
    {
        var e = new Escenario();

        await e.Servicio.EjecutarAsync(e.Token());

        var registro = Assert.Single(e.Habito.Registros);
        Assert.Equal(1m, registro.Cantidad);
        Assert.Equal(OrigenRegistro.Correo, registro.Origen);
        Assert.Equal("correo:2026-11-13", registro.ClaveIdempotencia);
        Assert.False(registro.EsVersionMinima);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Ejecutar_DosVeces_NoDuplica()
    {
        var e = new Escenario();
        var token = e.Token();

        await e.Servicio.EjecutarAsync(token);
        await e.Servicio.EjecutarAsync(token);

        Assert.Single(e.Habito.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-09")]
    public async Task RF_NEG_07_Ejecutar_SiLaBaseRechazaElDuplicado_ElSegundoClicEsUnExito()
    {
        var e = new Escenario();
        e.Unidad.Conflicto = true; // el otro clic ganó la carrera

        var excepcion = await Record.ExceptionAsync(() => e.Servicio.EjecutarAsync(e.Token()));

        Assert.Null(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-11")]
    public async Task RF_NEG_07_Ejecutar_UnEnlaceDeAyer_CuentaParaAyerAlMediodiaLocal()
    {
        var e = new Escenario();
        var ayer = Hoy.AddDays(-1);

        await e.Servicio.EjecutarAsync(e.Token(fecha: ayer));

        // Mediodía local del 12-nov en Santo Domingo = 16:00 UTC.
        Assert.Equal(new DateTime(2026, 11, 12, 16, 0, 0, DateTimeKind.Utc), Assert.Single(e.Habito.Registros).Fecha);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-11")]
    public async Task RF_NEG_07_Ejecutar_UnEnlaceDeHoyAntesDelMediodia_CuentaAhora_NoEnElFuturo()
    {
        var e = new Escenario(); // 10:00 locales: el mediodía de hoy todavía no llegó

        await e.Servicio.EjecutarAsync(e.Token());

        Assert.Equal(Viernes, Assert.Single(e.Habito.Registros).Fecha);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_07_Ejecutar_FechaFutura_EsEnlaceNoValido()
    {
        var e = new Escenario();

        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(e.Token(fecha: Hoy.AddDays(1))));
        Assert.Empty(e.Habito.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_07_Ejecutar_TokenCaducado_EsEnlaceNoValido()
    {
        var e = new Escenario();
        var token = e.Token();
        e.Reloj.Ahora = Viernes.AddHours(37);

        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(token));
        Assert.Empty(e.Habito.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_07_Ejecutar_ElHabitoDeOtraPersona_EsEnlaceNoValido()
    {
        var e = new Escenario();
        e.Directorio.Activos.Add(Otro);
        // Un token firmado para otra persona con el hábito de Usuario.
        var token = e.Token(usuario: Otro);

        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(token));
        Assert.Empty(e.Habito.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_07_Ejecutar_UnHabitoQueNoExiste_EsEnlaceNoValido()
    {
        var e = new Escenario();

        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(e.Token(habito: Guid.NewGuid())));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_07_Ejecutar_CuentaDesactivadaOEnBaja_EsEnlaceNoValido()
    {
        var e = new Escenario();
        var token = e.Token();
        e.Directorio.Activos.Clear();

        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(token));
        Assert.Empty(e.Habito.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_07_Ejecutar_PlanPausadoOCancelado_EsEnlaceNoValido()
    {
        var e = new Escenario();
        var token = e.Token();

        e.Plan.Pausar(MotivoPausa.Viaje, Viernes);
        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(token));

        e.Plan.Reanudar(Viernes, TimeZoneInfo.Utc);
        e.Plan.Cancelar(Viernes);
        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(token));
        Assert.Empty(e.Habito.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_07_Ejecutar_HabitoPausado_EsEnlaceNoValido()
    {
        var e = new Escenario();
        var token = e.Token();
        e.Habito.Pausar(Usuario);

        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(token));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    [Trait("Requirement", "RD-02")]
    public async Task RF_NEG_07_Ejecutar_UnaTareaQueNoEsDeCantidad1_NoSeRegistraPorCorreo()
    {
        var e = new Escenario(cantidad: 8); // «8 vasos»: eso se registra desde la app

        await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(e.Token()));
        Assert.Empty(e.Habito.Registros);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_TodosLosRechazos_DicenLoMismo_SinRevelarLaRazon()
    {
        var e = new Escenario();
        e.Directorio.Activos.Clear();

        var porCuenta = await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync(e.Token()));
        var porBasura = await Assert.ThrowsAsync<EnlaceNoValidoException>(() => e.Servicio.EjecutarAsync("basura"));

        Assert.Equal(porCuenta.Message, porBasura.Message);
    }
}
