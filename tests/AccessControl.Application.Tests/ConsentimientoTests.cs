using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AccessControl.Application.Tests;

/// <summary>
/// Consentimiento legal versionado (RF-CA-01, RD-07, RD-11): historial de solo inserción de lo que cada usuario aceptó,
/// versiones vigentes que se validan en el servidor y fecha de aceptación que fija siempre el servidor.
/// </summary>
public class ConsentimientoTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly DateTime Ahora = DateTime.UtcNow;

    // ====================== La entidad ======================

    [Fact]
    public void RF_CA_01_ElConsentimientoDelRegistro_AceptaSoloLosDocumentosYElMarketingEsOpcional()
    {
        var c = ConsentimientoUsuario.DelRegistro(UsuarioId, "v2.1", "v1.4", marketing: true, Ahora);

        Assert.Equal(ConsentimientoUsuario.OrigenRegistro, c.Origen);
        Assert.True(c.Marketing);
        // Los datos de salud y el resto se consienten después, en el onboarding: nunca se asumen en el registro.
        Assert.False(c.DatosSalud);
        Assert.False(c.CompartirCumplimiento);
        Assert.False(c.EstadisticasAnonimas);
        Assert.Equal(Ahora, c.AceptadoEn);
        Assert.Equal(Ahora, c.ActualizadoEn);
    }

    [Theory]
    [InlineData("", "v1.4", "registro")]
    [InlineData("  ", "v1.4", "registro")]
    [InlineData("v2.1", "", "registro")]
    [InlineData("0123456789012345678901", "v1.4", "registro")]   // 22 caracteres: no cabe en la columna
    [InlineData("v2.1", "v1.4", "otro")]
    [InlineData("v2.1", "v1.4", "")]
    public void RD_07_ElConsentimientoRechazaVersionesVaciasLargasOUnOrigenDesconocido(string terminos, string privacidad, string origen)
    {
        Assert.Throws<ArgumentException>(() =>
            new ConsentimientoUsuario(UsuarioId, terminos, privacidad, false, false, false, false, Ahora, Ahora, origen));
    }

    [Fact]
    public void RD_11_LaAceptacionNoPuedeSerPosteriorAlMomentoActual()
    {
        Assert.Throws<ArgumentException>(() =>
            new ConsentimientoUsuario(UsuarioId, "v2.1", "v1.4", false, false, false, false, Ahora.AddMinutes(5), Ahora, "registro"));
    }

    [Fact]
    public void RD_07_UnUsuarioVacioSeRechaza()
    {
        Assert.Throws<ArgumentException>(() => ConsentimientoUsuario.DelRegistro(Guid.Empty, "v2.1", "v1.4", false, Ahora));
    }

    // ====================== El servicio ======================

    private sealed class RepositorioEnMemoria : IConsentimientoRepository
    {
        public List<ConsentimientoUsuario> Filas { get; } = new();

        public Task<ConsentimientoUsuario?> ObtenerVigenteAsync(Guid usuarioId) =>
            Task.FromResult(Filas.Where(f => f.UsuarioId == usuarioId).OrderByDescending(f => f.ActualizadoEn).FirstOrDefault());

        public Task AgregarAsync(ConsentimientoUsuario consentimiento)
        {
            Filas.Add(consentimiento);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ConsentimientoUsuario>> ListarHistorialAsync(Guid usuarioId) =>
            Task.FromResult<IReadOnlyList<ConsentimientoUsuario>>(
                Filas.Where(f => f.UsuarioId == usuarioId).OrderBy(f => f.ActualizadoEn).ToList());
    }

    private static (ConsentimientoService Servicio, RepositorioEnMemoria Repo) Crear(string terminos = "v2.1", string privacidad = "v1.4")
    {
        var repo = new RepositorioEnMemoria();
        return (new ConsentimientoService(repo, new VersionesLegales(terminos, privacidad)), repo);
    }

    private static GuardarConsentimientosRequest Solicitud(
        string terminos = "v2.1", string privacidad = "v1.4", bool salud = true, bool marketing = false) =>
        new(terminos, privacidad, salud, marketing, false, true);

    private static ConsentimientoUsuario Previo(string terminos, string privacidad, DateTime aceptadoEn, DateTime ahora) =>
        new(UsuarioId, terminos, privacidad, false, false, false, false, aceptadoEn, ahora, ConsentimientoUsuario.OrigenRegistro);

    [Fact]
    public async Task RD_11_LaFechaDeAceptacionLaFijaElServidorYElOrigenEsActualizacion()
    {
        var (servicio, repo) = Crear();

        var guardado = await servicio.GuardarAsync(UsuarioId, Solicitud());

        var fila = Assert.Single(repo.Filas);
        Assert.Equal(ConsentimientoUsuario.OrigenActualizacion, fila.Origen);
        Assert.InRange((DateTime.UtcNow - fila.AceptadoEn).TotalSeconds, -1, 5);
        Assert.Equal(DateTimeKind.Utc, guardado.AceptadoEn!.Value.Kind);
        Assert.True(guardado.DatosSalud);
        Assert.True(guardado.EstadisticasAnonimas);
    }

    [Theory]
    [InlineData("v1.0", "v1.4")]
    [InlineData("v2.1", "v9.9")]
    [InlineData("v3.0", "v1.4")]
    [InlineData("", "v1.4")]
    public async Task RD_07_UnaVersionQueNoEsLaVigenteNiLaYaAceptada_SeRechazaYNoSeGuardaNada(string terminos, string privacidad)
    {
        var (servicio, repo) = Crear();

        var error = await Assert.ThrowsAsync<ConsentimientoNoValidoException>(
            () => servicio.GuardarAsync(UsuarioId, Solicitud(terminos, privacidad)));

        Assert.Equal("consentimiento_no_valido", error.Codigo);
        Assert.Empty(repo.Filas);
    }

    [Fact]
    public async Task RD_07_FaltaUnInterruptor_SeRechazaEnLugarDeSuponerFalso()
    {
        var (servicio, repo) = Crear();

        await Assert.ThrowsAsync<ArgumentException>(
            () => servicio.GuardarAsync(UsuarioId, new GuardarConsentimientosRequest("v2.1", "v1.4", true, null, false, false)));

        Assert.Empty(repo.Filas);
    }

    [Fact]
    public async Task RF_CA_01_QuienYaAceptoUnaVersionAnterior_PuedeCambiarUnInterruptorSinAceptarLaNueva()
    {
        // Las versiones vigentes subieron a v3.0/v2.0, pero la persona había aceptado v2.1/v1.4: mover un interruptor
        // no debe obligarla a aceptar documentos que aún no ha visto.
        var (servicio, repo) = Crear("v3.0", "v2.0");
        var hace = DateTime.UtcNow.AddDays(-30);
        repo.Filas.Add(Previo("v2.1", "v1.4", hace, hace));

        var guardado = await servicio.GuardarAsync(UsuarioId, Solicitud("v2.1", "v1.4", marketing: true));

        Assert.Equal("v2.1", guardado.TerminosVersion);
        Assert.Equal(2, repo.Filas.Count);
        Assert.True(guardado.Marketing);
        // La aceptación de esas versiones sigue siendo la de hace 30 días.
        Assert.Equal(hace, repo.Filas.Last().AceptadoEn);
        Assert.True(await servicio.RequiereAceptarDocumentosAsync(UsuarioId));
    }

    [Fact]
    public async Task RF_CA_01_LasMismasVersiones_ConservanLaFechaDeAceptacionYAgreganUnaFilaAlHistorial()
    {
        var (servicio, repo) = Crear();
        var hace = DateTime.UtcNow.AddDays(-10);
        repo.Filas.Add(Previo("v2.1", "v1.4", hace, hace));

        await servicio.GuardarAsync(UsuarioId, Solicitud(marketing: true));

        Assert.Equal(2, repo.Filas.Count);                           // historial de solo inserción
        Assert.Equal(hace, repo.Filas[0].AceptadoEn);
        Assert.Equal(hace, repo.Filas[1].AceptadoEn);                // misma aceptación
        Assert.True(repo.Filas[1].ActualizadoEn > repo.Filas[0].ActualizadoEn);
    }

    [Fact]
    public async Task RF_CA_01_UnaVersionNueva_RenuevaLaFechaDeAceptacion()
    {
        var (servicio, repo) = Crear("v3.0", "v1.4");
        var hace = DateTime.UtcNow.AddDays(-10);
        repo.Filas.Add(Previo("v2.1", "v1.4", hace, hace));

        await servicio.GuardarAsync(UsuarioId, Solicitud("v3.0", "v1.4"));

        Assert.InRange((DateTime.UtcNow - repo.Filas.Last().AceptadoEn).TotalSeconds, -1, 5);
        Assert.False(await servicio.RequiereAceptarDocumentosAsync(UsuarioId));
    }

    [Fact]
    public async Task RF_CA_01_ObtenerSinConsentimientos_DevuelveNull_YConConsentimientosLosMapeaEnUtc()
    {
        var (servicio, repo) = Crear();
        Assert.Null(await servicio.ObtenerAsync(UsuarioId));

        repo.Filas.Add(Previo("v2.1", "v1.4", DateTime.SpecifyKind(Ahora, DateTimeKind.Unspecified), Ahora));
        var dto = await servicio.ObtenerAsync(UsuarioId);

        Assert.Equal("v2.1", dto!.TerminosVersion);
        // SQL Server devuelve fechas sin zona: el DTO las marca UTC para que el JSON lleve la "Z".
        Assert.Equal(DateTimeKind.Utc, dto.AceptadoEn!.Value.Kind);
    }

    [Fact]
    public async Task RF_CA_01_ElHistorialDeOtroUsuarioNoSeMezcla()
    {
        var (servicio, repo) = Crear();
        repo.Filas.Add(new ConsentimientoUsuario(Guid.NewGuid(), "v2.1", "v1.4", true, true, true, true, Ahora, Ahora, "registro"));

        Assert.Null(await servicio.ObtenerAsync(UsuarioId));
        Assert.True(await servicio.RequiereAceptarDocumentosAsync(UsuarioId));
    }

    [Theory]
    [InlineData("v2.1", "v1.4", false)]
    [InlineData("v2.0", "v1.4", true)]    // términos desactualizados
    [InlineData("v2.1", "v1.3", true)]    // privacidad desactualizada
    [InlineData("v2.0", "v1.3", true)]
    public async Task RF_CA_01_RequiereAceptarDocumentos_SoloSiLaUltimaAceptacionNoEsDeLasVersionesVigentes(string terminos, string privacidad, bool esperado)
    {
        var (servicio, repo) = Crear();
        repo.Filas.Add(Previo(terminos, privacidad, Ahora, Ahora));

        Assert.Equal(esperado, await servicio.RequiereAceptarDocumentosAsync(UsuarioId));
    }

    [Fact]
    public async Task RF_CA_01_UnaCuentaSinNingunConsentimiento_DebeAceptarLosDocumentos()
    {
        var (servicio, _) = Crear();

        Assert.True(await servicio.RequiereAceptarDocumentosAsync(UsuarioId));
    }

    // ====================== En el registro ======================

    private static (UsuarioService Servicio, Mock<IUsuarioRepository> Repo) CrearServicioDeUsuarios(IVersionesLegales? versiones = null)
    {
        var repo = new Mock<IUsuarioRepository>();
        repo.Setup(r => r.CorreoExistsAsync(It.IsAny<Email>())).ReturnsAsync(false);
        repo.Setup(r => r.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>())).Returns(Task.CompletedTask);
        var hasher = new Mock<IPasswordHasher>();
        hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("hash");
        var cola = new Mock<IEmailQueue>();
        cola.Setup(c => c.EncolarCorreoActivacionAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var servicio = new UsuarioService(
            repo.Object, hasher.Object, cola.Object, Mock.Of<IAuditoriaService>(), Mock.Of<IJwtProvider>(),
            GestorSesionesDePrueba.Crear(), Mock.Of<ILogger<UsuarioService>>(), politica: null, versiones);
        return (servicio, repo);
    }

    private static RegistroRequestDto Registro(ConsentimientosRegistroDto? consentimientos) =>
        new("Persona", "persona@ejemplo.com", "ClaveSegura-Nexus-2026", consentimientos);

    public static TheoryData<ConsentimientosRegistroDto?> RegistrosSinConsentimientoValido => new()
    {
        { null },
        { new ConsentimientosRegistroDto("v2.1", "v1.4", null) },        // falta decidir el marketing
        { new ConsentimientosRegistroDto("v2.0", "v1.4", false) },       // términos desactualizados
        { new ConsentimientosRegistroDto("v2.1", "v1.3", false) },       // privacidad desactualizada
        { new ConsentimientosRegistroDto("", "", false) },
    };

    [Theory]
    [MemberData(nameof(RegistrosSinConsentimientoValido))]
    public async Task RF_CA_01_SinLaAceptacionDeLasVersionesVigentes_NoSeCreaLaCuentaNiSeTocaLaBase(ConsentimientosRegistroDto? consentimientos)
    {
        var (servicio, repo) = CrearServicioDeUsuarios();

        var error = await Assert.ThrowsAsync<ConsentimientoNoValidoException>(() => servicio.RegistrarUsuarioAsync(Registro(consentimientos)));

        Assert.Equal("consentimiento_no_valido", error.Codigo);
        repo.Verify(r => r.CorreoExistsAsync(It.IsAny<Email>()), Times.Never);
        repo.Verify(r => r.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RF_CA_01_ElRegistroGuardaElUsuarioYSuConsentimientoJuntos(bool marketing)
    {
        var (servicio, repo) = CrearServicioDeUsuarios();
        Usuario? usuario = null;
        ConsentimientoUsuario? consentimiento = null;
        repo.Setup(r => r.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>()))
            .Callback<Usuario, ConsentimientoUsuario>((u, c) => { usuario = u; consentimiento = c; })
            .Returns(Task.CompletedTask);

        await servicio.RegistrarUsuarioAsync(Registro(new ConsentimientosRegistroDto("v2.1", "v1.4", marketing)));

        repo.Verify(r => r.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>()), Times.Once);
        Assert.NotNull(usuario);
        Assert.Equal(usuario!.Id, consentimiento!.UsuarioId);
        Assert.Equal("v2.1", consentimiento.TerminosVersion);
        Assert.Equal("v1.4", consentimiento.PrivacidadVersion);
        Assert.Equal(marketing, consentimiento.Marketing);
        Assert.False(consentimiento.DatosSalud);
        Assert.Equal(ConsentimientoUsuario.OrigenRegistro, consentimiento.Origen);
        // RD-11: el instante de aceptación lo pone el servidor, en UTC.
        Assert.InRange((DateTime.UtcNow - consentimiento.AceptadoEn).TotalSeconds, -1, 5);
    }

    [Fact]
    public async Task RF_CA_01_LasVersionesVigentesSonConfigurables_YLasAntiguasDejanDeAceptarse()
    {
        var (servicio, repo) = CrearServicioDeUsuarios(new VersionesLegales("v3.0", "v2.0"));

        await Assert.ThrowsAsync<ConsentimientoNoValidoException>(
            () => servicio.RegistrarUsuarioAsync(Registro(new ConsentimientosRegistroDto("v2.1", "v1.4", false))));
        await servicio.RegistrarUsuarioAsync(Registro(new ConsentimientosRegistroDto("v3.0", "v2.0", false)));

        repo.Verify(r => r.SaveAsync(It.IsAny<Usuario>(), It.IsAny<ConsentimientoUsuario>()), Times.Once);
    }
}
