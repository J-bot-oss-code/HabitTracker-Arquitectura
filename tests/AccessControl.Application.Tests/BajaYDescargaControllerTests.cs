using System.Reflection;
using System.Security.Claims;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Excepciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moq;
using WebApi.Controllers;
using WebApi.Extensions;

namespace AccessControl.Application.Tests;

/// <summary>Endpoints de baja de cuenta y descarga de datos: identidad del token, rol Estandar y límites (RD-06, RF-CA-20).</summary>
public class BajaYDescargaControllerTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private readonly Mock<IServicioPrivacidad> _privacidad = new();

    private PrivacidadController Controlador(Guid? usuarioId = null)
    {
        var claims = usuarioId is null ? [] : new[] { new Claim(ClaimTypes.NameIdentifier, usuarioId.Value.ToString()) };
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "prueba")) };
        return new PrivacidadController(Mock.Of<IConsentimientoService>(), _privacidad.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    // ---------------- Solicitar la baja ----------------

    [Fact]
    public async Task RF_CA_20_SolicitarLaBajaResponde202ConElModoYLaFecha()
    {
        var borradoEn = DateTime.UtcNow.AddDays(30);
        _privacidad.Setup(p => p.SolicitarBajaAsync(UsuarioId, "eliminar", "clave", null))
            .ReturnsAsync(new BajaSolicitadaDto("eliminar", borradoEn));

        var resultado = await Controlador(UsuarioId).SolicitarBaja(new SolicitarBajaRequest("eliminar", "clave"));

        var aceptada = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(202, aceptada.StatusCode);
        var cuerpo = Assert.IsType<BajaSolicitadaDto>(aceptada.Value);
        Assert.Equal("eliminar", cuerpo.Modo);
        Assert.Equal(borradoEn, cuerpo.BorradoEn);
    }

    [Fact]
    public async Task RD_06_LaBajaSeSolicitaSiempreParaLaIdentidadDelToken()
    {
        _privacidad.Setup(p => p.SolicitarBajaAsync(UsuarioId, "desactivar", null, "id-token"))
            .ReturnsAsync(new BajaSolicitadaDto("desactivar", null));

        await Controlador(UsuarioId).SolicitarBaja(new SolicitarBajaRequest("desactivar", IdTokenGoogle: "id-token"));

        _privacidad.Verify(p => p.SolicitarBajaAsync(UsuarioId, "desactivar", null, "id-token"), Times.Once);
        _privacidad.Verify(p => p.SolicitarBajaAsync(It.Is<Guid>(g => g != UsuarioId), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_19_UnaConfirmacionIncorrectaSePropagaComoRechazoDeNegocio_NoComoUnAutenticacion()
    {
        _privacidad.Setup(p => p.SolicitarBajaAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ThrowsAsync(new ClaveActualIncorrectaException());

        var error = await Assert.ThrowsAsync<ClaveActualIncorrectaException>(
            () => Controlador(UsuarioId).SolicitarBaja(new SolicitarBajaRequest("eliminar", "mala")));

        // ControlAccesoException = 400 en el middleware; nunca UnauthorizedAccessException (401/403 cerraría la sesión).
        Assert.IsNotType<UnauthorizedAccessException>(error);
        Assert.IsAssignableFrom<ControlAccesoException>(error);
    }

    // ---------------- Consultar y cancelar ----------------

    [Fact]
    public async Task RF_CA_20_ConsultarLaBajaDevuelveLaFechaOUn404()
    {
        var fecha = DateTime.UtcNow.AddDays(30);
        _privacidad.Setup(p => p.ObtenerBajaProgramadaAsync(UsuarioId)).ReturnsAsync(fecha);

        var ok = await Controlador(UsuarioId).ObtenerBaja();
        Assert.IsType<OkObjectResult>(ok);

        _privacidad.Setup(p => p.ObtenerBajaProgramadaAsync(UsuarioId)).ReturnsAsync((DateTime?)null);
        var noEncontrado = await Controlador(UsuarioId).ObtenerBaja();
        Assert.Equal(404, Assert.IsType<ProblemDetails>(Assert.IsType<NotFoundObjectResult>(noEncontrado).Value).Status);
    }

    [Fact]
    public async Task RF_CA_20_CancelarLaBajaResponde204OUn404SiNoHabiaNinguna()
    {
        _privacidad.Setup(p => p.CancelarBajaAsync(UsuarioId)).ReturnsAsync(true);
        Assert.IsType<NoContentResult>(await Controlador(UsuarioId).CancelarBaja());

        _privacidad.Setup(p => p.CancelarBajaAsync(UsuarioId)).ReturnsAsync(false);
        Assert.IsType<NotFoundObjectResult>(await Controlador(UsuarioId).CancelarBaja());
    }

    // ---------------- Descarga ----------------

    [Fact]
    public async Task RF_CA_07_LaDescargaEntregaUnJsonQueNoSeGuardaEnCachesYSeBajaComoArchivo()
    {
        var datos = new ExportacionDatosDto(
            new DateTime(2026, 11, 5, 12, 0, 0, DateTimeKind.Utc),
            new CuentaExportadaDto(UsuarioId, "Persona", "persona@ejemplo.com", "Estandar", true, DateTime.UtcNow, true, null, null),
            [], [], [], new Dictionary<string, object?>());
        _privacidad.Setup(p => p.ExportarAsync(UsuarioId, It.IsAny<CancellationToken>())).ReturnsAsync(datos);
        var controlador = Controlador(UsuarioId);

        var resultado = await controlador.Exportar(CancellationToken.None);

        Assert.Same(datos, Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("no-store", controlador.Response.Headers.CacheControl.ToString());
        Assert.Contains("attachment", controlador.Response.Headers.ContentDisposition.ToString());
        Assert.Contains("20261105", controlador.Response.Headers.ContentDisposition.ToString());
    }

    // ---------------- Autorización ----------------

    [Fact]
    public async Task RD_06_SinIdentidadEnElTokenNingunEndpointDeBajaLlegaAlServicio()
    {
        var controlador = Controlador(usuarioId: null);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.SolicitarBaja(new SolicitarBajaRequest("eliminar", "x")));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.ObtenerBaja());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.CancelarBaja());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.Exportar(CancellationToken.None));

        _privacidad.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(nameof(PrivacidadController.SolicitarBaja))]
    [InlineData(nameof(PrivacidadController.ObtenerBaja))]
    [InlineData(nameof(PrivacidadController.CancelarBaja))]
    [InlineData(nameof(PrivacidadController.Exportar))]
    public void RD_06_LaBajaYLaDescargaSonSoloParaElRolEstandar(string metodo)
    {
        var autorizacion = typeof(PrivacidadController).GetMethod(metodo)!.GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(autorizacion);
        Assert.Equal("Estandar", autorizacion!.Roles);
    }

    [Theory]
    [InlineData(nameof(PrivacidadController.ObtenerConsentimientos))]
    [InlineData(nameof(PrivacidadController.GuardarConsentimientos))]
    public void RF_CA_01_LosConsentimientosSiguenAbiertosACualquierUsuarioAutenticado(string metodo)
    {
        Assert.Null(typeof(PrivacidadController).GetMethod(metodo)!.GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(typeof(PrivacidadController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Theory]
    [InlineData(nameof(PrivacidadController.SolicitarBaja))]
    [InlineData(nameof(PrivacidadController.Exportar))]
    public void RD_06_LaBajaYLaDescargaTienenSuPropioLimiteDePeticiones(string metodo)
    {
        var limite = typeof(PrivacidadController).GetMethod(metodo)!.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.Equal(LimitacionDePeticionesExtensions.Privacidad, limite!.PolicyName);
    }

    [Fact]
    public void RD_06_LasRutasDeBajaYDescargaCoincidenConElContratoDelFrontend()
    {
        string Ruta(string metodo) => typeof(PrivacidadController).GetMethod(metodo)!
            .GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single().Template!;

        Assert.Equal("api/privacy", typeof(PrivacidadController).GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.Equal("deletion-request", Ruta(nameof(PrivacidadController.SolicitarBaja)));
        Assert.Equal("deletion-request", Ruta(nameof(PrivacidadController.ObtenerBaja)));
        Assert.Equal("deletion-request", Ruta(nameof(PrivacidadController.CancelarBaja)));
        Assert.Equal("export", Ruta(nameof(PrivacidadController.Exportar)));
    }
}
