using AccessControl.Application.Service;
using System.Reflection;
using System.Security.Claims;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace AccessControl.Application.Tests;

/// <summary>Endpoints de consentimientos y aviso de re-aceptación en el login y en /me (RD-06: la identidad sale del token).</summary>
public class PrivacidadControllerTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static ControllerContext ContextoDe(Guid? usuarioId)
    {
        var claims = usuarioId is null ? [] : new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.Value.ToString()) };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba")) };
        return new ControllerContext { HttpContext = http };
    }

    private static PrivacidadController CrearPrivacidad(Mock<IConsentimientoService> servicio, Guid? usuarioId = null) =>
        new(servicio.Object, Mock.Of<IServicioPrivacidad>()) { ControllerContext = ContextoDe(usuarioId ?? UsuarioId) };

    private static ConsentimientosDto Dto() => new("v2.1", "v1.4", DateTime.UtcNow, true, false, false, true);

    private static GuardarConsentimientosRequest Solicitud() => new("v2.1", "v1.4", true, false, false, true);

    // ---------------- Autorización ----------------

    [Fact]
    public void RD_06_ElControladorDePrivacidadExigeUnUsuarioAutenticado()
    {
        Assert.NotNull(typeof(PrivacidadController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/privacy", typeof(PrivacidadController).GetCustomAttribute<RouteAttribute>()!.Template);
    }

    [Fact]
    public async Task RD_06_ConUnTokenSinIdentidad_SeRechazaSinLlegarAlServicio()
    {
        var servicio = new Mock<IConsentimientoService>();
        var controlador = CrearPrivacidad(servicio, Guid.Empty);
        controlador.ControllerContext = ContextoDe(null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.ObtenerConsentimientos());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.GuardarConsentimientos(Solicitud()));

        servicio.VerifyNoOtherCalls();
    }

    // ---------------- GET / PUT ----------------

    [Fact]
    public async Task RF_CA_01_SinConsentimientos_ElGetDevuelve404()
    {
        var servicio = new Mock<IConsentimientoService>();
        servicio.Setup(s => s.ObtenerAsync(UsuarioId)).ReturnsAsync((ConsentimientosDto?)null);

        var resultado = await CrearPrivacidad(servicio).ObtenerConsentimientos();

        var noEncontrado = Assert.IsType<NotFoundObjectResult>(resultado);
        Assert.Equal(404, Assert.IsType<ProblemDetails>(noEncontrado.Value).Status);
    }

    [Fact]
    public async Task RF_CA_01_ConConsentimientos_ElGetLosDevuelve_ConLaIdentidadDelToken()
    {
        var servicio = new Mock<IConsentimientoService>();
        servicio.Setup(s => s.ObtenerAsync(UsuarioId)).ReturnsAsync(Dto());

        var resultado = await CrearPrivacidad(servicio).ObtenerConsentimientos();

        Assert.Equal("v2.1", Assert.IsType<ConsentimientosDto>(Assert.IsType<OkObjectResult>(resultado).Value).TerminosVersion);
    }

    [Fact]
    public async Task RF_CA_01_ElPutGuardaParaElUsuarioDelToken_NuncaParaOtro()
    {
        var servicio = new Mock<IConsentimientoService>();
        servicio.Setup(s => s.GuardarAsync(UsuarioId, It.IsAny<GuardarConsentimientosRequest>())).ReturnsAsync(Dto());

        var resultado = await CrearPrivacidad(servicio).GuardarConsentimientos(Solicitud());

        Assert.IsType<OkObjectResult>(resultado);
        servicio.Verify(s => s.GuardarAsync(UsuarioId, It.IsAny<GuardarConsentimientosRequest>()), Times.Once);
        servicio.Verify(s => s.GuardarAsync(It.Is<Guid>(g => g != UsuarioId), It.IsAny<GuardarConsentimientosRequest>()), Times.Never);
    }

    // ---------------- Aviso de re-aceptación ----------------

    private static IGestorSesiones SesionesQueEmiten()
    {
        var sesiones = new Mock<IGestorSesiones>();
        sesiones.Setup(x => x.IniciarAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new SesionEmitida("refresh-de-prueba", DateTime.UtcNow.AddDays(14)));
        return sesiones.Object;
    }

    private static AuthController CrearAuth(Mock<IUsuarioService> usuarios, Mock<IConsentimientoService> consentimientos, Guid? usuarioId = null) =>
        new(usuarios.Object, Mock.Of<ITokenBlacklist>(), consentimientos.Object, SesionesQueEmiten(), OpcionesSesion.PorDefecto, Mock.Of<IServicioLoginExterno>()) { ControllerContext = ContextoDe(usuarioId) };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RF_CA_01_ElLoginAvisaSiHayQueVolverAAceptarLosDocumentos(bool requiere)
    {
        var usuarios = new Mock<IUsuarioService>();
        usuarios.Setup(u => u.AutenticarUsuarioAsync(It.IsAny<LoginRequestDto>()))
            .ReturnsAsync(new UsuarioResponseDto(UsuarioId, "Persona", "persona@ejemplo.com", "Estandar", true, "jwt"));
        var consentimientos = new Mock<IConsentimientoService>();
        consentimientos.Setup(c => c.RequiereAceptarDocumentosAsync(UsuarioId)).ReturnsAsync(requiere);

        var resultado = await CrearAuth(usuarios, consentimientos).Login(new LoginRequestDto("persona@ejemplo.com", "ClaveSegura-Nexus-2026"));

        var respuesta = Assert.IsType<UsuarioResponseDto>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(requiere, respuesta.RequiereAceptarDocumentos);
        Assert.Equal("jwt", respuesta.Token);   // el inicio de sesión funciona igual: no se bloquea a nadie
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RF_CA_07_MeIncluyeElAvisoDeReaceptacion(bool requiere)
    {
        var consentimientos = new Mock<IConsentimientoService>();
        consentimientos.Setup(c => c.RequiereAceptarDocumentosAsync(UsuarioId)).ReturnsAsync(requiere);

        var resultado = await CrearAuth(new Mock<IUsuarioService>(), consentimientos, UsuarioId).Me();

        var cuerpo = Assert.IsType<OkObjectResult>(resultado).Value!;
        Assert.Equal(requiere, (bool)cuerpo.GetType().GetProperty("requiereAceptarDocumentos")!.GetValue(cuerpo)!);
    }

    [Fact]
    public async Task RF_CA_07_MeSinIdentidadValida_NoConsultaElServicio()
    {
        var consentimientos = new Mock<IConsentimientoService>();

        await CrearAuth(new Mock<IUsuarioService>(), consentimientos, usuarioId: null).Me();

        consentimientos.VerifyNoOtherCalls();
    }
}
