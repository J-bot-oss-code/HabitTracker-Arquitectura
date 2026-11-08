using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Progreso;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Application.Tests;

/// <summary>Servicios de perfil, cuestionario y progreso corporal con dobles en memoria (sin base de datos ni API).</summary>
public class PerfilSaludServicesTests
{
    private static readonly DateTime Ahora = new(2026, 11, 8, 15, 0, 0, DateTimeKind.Utc);
    private readonly Guid usuario = Guid.NewGuid();
    private readonly Perfiles perfiles = new();
    private readonly Cuestionarios cuestionarios = new();
    private readonly Progreso progreso = new();
    private readonly UnidadDeTrabajo unidad = new();
    private readonly Consentimiento consentimiento = new();
    private readonly RelojFijo reloj = new(Ahora);

    private static DatosPerfil Nivel1() => new(PerfilLaboral.TurnoNocturno, "America/Santo_Domingo", true);

    private PerfilService PerfilService() => new(perfiles, consentimiento, unidad, reloj);
    private CuestionarioSaludService CuestionarioService() => new(cuestionarios, perfiles, consentimiento, unidad, reloj);
    private ProgresoCorporalService ProgresoService() => new(progreso, perfiles, consentimiento, unidad, reloj);

    // ---------- Perfil ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Perfil_SinOnboarding_RechazaConPerfilNoEncontrado()
    {
        var excepcion = await Record.ExceptionAsync(() => PerfilService().ObtenerAsync(usuario));

        var noEncontrado = Assert.IsType<RecursoNoEncontradoException>(excepcion);
        Assert.Equal(RecursoNoEncontradoException.PerfilNoEncontrado, noEncontrado.Codigo);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Perfil_Nivel1SinConsentimientoDeSalud_SeGuarda()
    {
        // Arrange: P1, el onboarding de 3 toques no pide datos de salud.
        consentimiento.DatosSalud = false;

        // Act
        var dto = await PerfilService().GuardarAsync(usuario, Nivel1());

        // Assert
        Assert.Equal(PerfilLaboral.TurnoNocturno, dto.PerfilLaboral);
        Assert.Equal(1, unidad.Guardados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_02_Perfil_ConPesoSinConsentimiento_Rechaza409YNoGuarda()
    {
        consentimiento.DatosSalud = false;

        var excepcion = await Record.ExceptionAsync(() => PerfilService().GuardarAsync(usuario, Nivel1() with { PesoKg = 70 }));

        var falta = Assert.IsType<ConsentimientoSaludRequeridoException>(excepcion);
        Assert.Equal("consentimiento_salud_requerido", falta.Codigo);
        Assert.Equal(0, unidad.Guardados);
        Assert.Empty(perfiles.Datos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Perfil_GuardarDosVeces_ReemplazaElMismoPerfil()
    {
        consentimiento.DatosSalud = true;
        await PerfilService().GuardarAsync(usuario, Nivel1());

        var dto = await PerfilService().GuardarAsync(usuario, Nivel1() with { PerfilLaboral = PerfilLaboral.Remoto, PesoKg = 70 });

        Assert.Single(perfiles.Datos);
        Assert.Equal(PerfilLaboral.Remoto, dto.PerfilLaboral);
        Assert.Equal(70, dto.PesoKg);
    }

    // ---------- Cuestionario ----------

    private static BloqueSeguridad SinAlertas() => new(false, false, false, false, false, false, false);

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_02_Cuestionario_SinConsentimiento_Rechaza409YNoGuarda()
    {
        consentimiento.DatosSalud = false;

        var excepcion = await Record.ExceptionAsync(() => CuestionarioService().GuardarSeguridadAsync(usuario, SinAlertas()));

        Assert.IsType<ConsentimientoSaludRequeridoException>(excepcion);
        Assert.Empty(cuestionarios.Datos);
        Assert.Equal(0, unidad.Guardados);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Cuestionario_GuardarUnBloque_NoTocaLosDemas()
    {
        // Arrange
        consentimiento.DatosSalud = true;
        var objetivos = new BloqueObjetivos([Objetivo.DormirMejor], [], Experiencia.Ninguna, Lugar.CasaSinEquipo);
        await CuestionarioService().GuardarObjetivosAsync(usuario, objetivos);

        // Act
        var dto = await CuestionarioService().GuardarSeguridadAsync(usuario, SinAlertas());

        // Assert
        Assert.NotNull(dto.Objetivos);
        Assert.NotNull(dto.Seguridad);
        Assert.Null(dto.Condiciones);
        Assert.Equal(new CompletitudCuestionario(true, true, false), dto.Evaluacion.Completitud);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Cuestionario_SinResponder_DevuelveBloquesVaciosYEvaluacionNormal()
    {
        var dto = await CuestionarioService().ObtenerAsync(usuario);

        Assert.Null(dto.Objetivos);
        Assert.Equal(NivelPrecaucion.Normal, dto.Evaluacion.Nivel);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Cuestionario_ConsentimientoRetirado_SeVePeroNoSeUsa()
    {
        // Arrange
        consentimiento.DatosSalud = true;
        await CuestionarioService().GuardarSeguridadAsync(usuario, new BloqueSeguridad(true, false, false, false, false, false, false));
        consentimiento.DatosSalud = false;

        // Act
        var dto = await CuestionarioService().ObtenerAsync(usuario);
        var evaluacion = await CuestionarioService().EvaluarAsync(usuario);

        // Assert: la persona sigue viendo sus respuestas (derecho de acceso), pero la evaluación ya no las usa.
        Assert.NotNull(dto.Seguridad);
        Assert.False(dto.UsaDatosDeSalud);
        Assert.Equal(NivelPrecaucion.Normal, dto.Evaluacion.Nivel);
        Assert.Equal(NivelPrecaucion.Normal, evaluacion.Nivel);
    }

    // ---------- Progreso corporal ----------

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_02_Peso_SinConsentimiento_Rechaza409()
    {
        consentimiento.DatosSalud = false;

        var excepcion = await Record.ExceptionAsync(() => ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora, 70)));

        Assert.IsType<ConsentimientoSaludRequeridoException>(excepcion);
        Assert.Empty(progreso.Pesos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Peso_DosVecesElMismoDia_QuedaUnoYPasaAlPerfil()
    {
        // Arrange
        consentimiento.DatosSalud = true;
        await PerfilService().GuardarAsync(usuario, Nivel1());

        // Act
        await ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora.AddHours(-3), 71));
        var pesos = await ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora, 70.5m));

        // Assert
        var registro = Assert.Single(pesos.Registros);
        Assert.Equal(70.5m, registro.PesoKg);
        Assert.Equal(70.5m, perfiles.Datos.Single().PesoKg);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Peso_DeUnDiaAnterior_NoPisaElPesoDelPerfil()
    {
        consentimiento.DatosSalud = true;
        await PerfilService().GuardarAsync(usuario, Nivel1());
        await ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora, 70));

        await ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora.AddDays(-5), 75));

        Assert.Equal(70, perfiles.Datos.Single().PesoKg);
        Assert.Equal(2, progreso.Pesos.Count);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Peso_ObjetivoOmitidoSeConservaYNullLoQuita()
    {
        // Arrange
        consentimiento.DatosSalud = true;
        await PerfilService().GuardarAsync(usuario, Nivel1());
        await ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora.AddDays(-2), 72, CambiarObjetivo: true, ObjetivoKg: 65));

        // Act
        var conservado = await ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora.AddDays(-1), 71));
        var quitado = await ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora, 70, CambiarObjetivo: true, ObjetivoKg: null));

        // Assert
        Assert.Equal(65, conservado.ObjetivoKg);
        Assert.Null(quitado.ObjetivoKg);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Peso_ObjetivoSinPerfil_RechazaConPerfilRequerido()
    {
        consentimiento.DatosSalud = true;

        var excepcion = await Record.ExceptionAsync(() =>
            ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora, 70, CambiarObjetivo: true, ObjetivoKg: 65)));

        var regla = Assert.IsType<ReglaNegocioException>(excepcion);
        Assert.Equal(ReglaNegocioException.PerfilRequerido, regla.Codigo);
        Assert.Empty(progreso.Pesos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Medicion_DosVecesElMismoDiaYZona_QuedaUna()
    {
        consentimiento.DatosSalud = true;

        await ProgresoService().RegistrarMedicionAsync(usuario, new RegistrarMedicionCommand(Ahora.AddHours(-1), ZonaMedicion.Cintura, 85));
        await ProgresoService().RegistrarMedicionAsync(usuario, new RegistrarMedicionCommand(Ahora, ZonaMedicion.Cintura, 84));
        await ProgresoService().RegistrarMedicionAsync(usuario, new RegistrarMedicionCommand(Ahora, ZonaMedicion.Brazo, 30));

        var mediciones = await ProgresoService().ObtenerMedicionesAsync(usuario);
        Assert.Equal(2, mediciones.Count);
        Assert.Equal(84, mediciones.Single(m => m.Zona == ZonaMedicion.Cintura).Valor);
    }

    // ---------- Descarga y baja ----------

    [Fact]
    [Trait("Requirement", "RD-09")]
    public async Task RD_09_DatosPerfilSalud_EliminarBorraTodoYEsIdempotente()
    {
        // Arrange
        consentimiento.DatosSalud = true;
        await PerfilService().GuardarAsync(usuario, Nivel1());
        await CuestionarioService().GuardarSeguridadAsync(usuario, SinAlertas());
        await ProgresoService().RegistrarPesoAsync(usuario, new RegistrarPesoCommand(Ahora, 70));
        var servicio = new DatosPerfilSaludService(perfiles, cuestionarios, progreso);
        var exportado = await servicio.ExportarAsync(usuario);

        // Act
        await servicio.EliminarAsync(usuario);
        await servicio.EliminarAsync(usuario);

        // Assert
        Assert.NotNull(exportado.Perfil);
        Assert.NotNull(exportado.Cuestionario);
        Assert.Single(exportado.Pesos);
        Assert.Empty(perfiles.Datos);
        Assert.Empty(cuestionarios.Datos);
        Assert.Empty(progreso.Pesos);
    }

    // ---------- Dobles ----------

    private sealed class Consentimiento : IConsentimientoSalud
    {
        public bool DatosSalud { get; set; }
        public Task<bool> TieneConsentimientoDatosSaludAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(DatosSalud);
    }

    private sealed class UnidadDeTrabajo : IUnitOfWork
    {
        public int Guardados { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { Guardados++; return Task.FromResult(1); }
        public void Dispose() { }
    }

    private sealed class Perfiles : IPerfilUsuarioRepository
    {
        public List<PerfilUsuario> Datos { get; } = [];
        public Task<PerfilUsuario?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Datos.FirstOrDefault(p => p.UsuarioId == usuarioId));
        public Task AgregarAsync(PerfilUsuario perfil, CancellationToken cancellationToken = default) { Datos.Add(perfil); return Task.CompletedTask; }
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Datos.RemoveAll(p => p.UsuarioId == usuarioId));
    }

    private sealed class Cuestionarios : ICuestionarioSaludRepository
    {
        public Dictionary<Guid, CuestionarioSalud> Datos { get; } = [];
        public Task<CuestionarioSalud?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Datos.GetValueOrDefault(usuarioId));
        public Task GuardarAsync(CuestionarioSalud cuestionario, CancellationToken cancellationToken = default)
        {
            Datos[cuestionario.UsuarioId] = cuestionario;
            return Task.CompletedTask;
        }
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Datos.Remove(usuarioId) ? 1 : 0);
    }

    private sealed class Progreso : IProgresoCorporalRepository
    {
        public List<RegistroPeso> Pesos { get; } = [];
        public List<MedicionCorporal> Mediciones { get; } = [];
        public Task<IReadOnlyList<RegistroPeso>> PesosAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RegistroPeso>>(Pesos.Where(p => p.UsuarioId == usuarioId).ToList());
        public Task AgregarPesoAsync(RegistroPeso registro, CancellationToken cancellationToken = default) { Pesos.Add(registro); return Task.CompletedTask; }
        public Task<IReadOnlyList<MedicionCorporal>> MedicionesAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MedicionCorporal>>(Mediciones.Where(m => m.UsuarioId == usuarioId).ToList());
        public Task<MedicionCorporal?> MedicionDelDiaAsync(Guid usuarioId, ZonaMedicion zona, DateTime diaUtc, CancellationToken cancellationToken = default) =>
            Task.FromResult(Mediciones.FirstOrDefault(m => m.UsuarioId == usuarioId && m.Zona == zona && m.Fecha >= diaUtc && m.Fecha < diaUtc.AddDays(1)));
        public Task AgregarMedicionAsync(MedicionCorporal medicion, CancellationToken cancellationToken = default) { Mediciones.Add(medicion); return Task.CompletedTask; }
        public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Pesos.RemoveAll(p => p.UsuarioId == usuarioId) + Mediciones.RemoveAll(m => m.UsuarioId == usuarioId));
    }

    private sealed class RelojFijo(DateTime ahoraUtc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(ahoraUtc, TimeSpan.Zero);
    }
}
