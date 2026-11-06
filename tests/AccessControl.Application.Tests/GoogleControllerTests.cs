using System.Reflection;
using System.Security.Claims;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Excepciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moq;
using WebApi.Controllers;
using WebApi.Extensions;

namespace AccessControl.Application.Tests;

/// <summary>Endpoints de Google: sesión y cookie como el login, códigos de error y autorización (RF-CA-03, RD-06).</summary>
public class GoogleControllerTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private readonly Mock<IServicioLoginExterno> _externo = new();
    private readonly Mock<IGestorSesiones> _sesiones = new();
    private readonly Mock<IConsentimientoService> _consentimientos = new();

    private static DefaultHttpContext Http(bool autenticado = false)
    {
        var claims = autenticado ? new[] { new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()) } : [];
        return new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, autenticado ? "prueba" : null)) };
    }

    private AuthController Controlador(DefaultHttpContext http) =>
        new(Mock.Of<IUsuarioService>(), Mock.Of<ITokenBlacklist>(), _consentimientos.Object, _sesiones.Object,
            OpcionesSesion.PorDefecto, _externo.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

    private static UsuarioResponseDto Usuario() => new(UsuarioId, "Persona", "persona@ejemplo.com", "Estandar", true, "jwt-de-acceso");

    private void SesionQueEmite() =>
        _sesiones.Setup(s => s.IniciarAsync(UsuarioId, It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new SesionEmitida("refresh-de-google", DateTime.UtcNow.AddDays(14)));

    private static GoogleLoginRequest Peticion(ConsentimientosRegistroDto? consentimientos = null) => new("id-token", consentimientos);

    // ---------------- POST /google ----------------

    [Fact]
    public async Task RF_CA_03_UnaIdentidadVinculadaRespondeConLaMismaSesionQueElLogin_200YCookie()
    {
        _externo.Setup(e => e.IniciarConGoogleAsync("id-token", null)).ReturnsAsync(new ResultadoLoginExterno(Usuario(), CuentaNueva: false));
        SesionQueEmite();
        _consentimientos.Setup(c => c.RequiereAceptarDocumentosAsync(UsuarioId)).ReturnsAsync(true);
        var http = Http();

        var resultado = await Controlador(http).Google(Peticion());

        var cuerpo = Assert.IsType<UsuarioResponseDto>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("jwt-de-acceso", cuerpo.Token);
        Assert.True(cuerpo.RequiereAceptarDocumentos);
        var cookie = http.Response.Headers.SetCookie.ToString().ToLowerInvariant();
        Assert.Contains($"{OpcionesSesion.NombreCookie}=refresh-de-google", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Fact]
    public async Task RF_CA_01_UnaCuentaNuevaRespondeConElMismoCuerpoPeroConEstado201()
    {
        var consentimientos = new ConsentimientosRegistroDto("v2.1", "v1.4", true);
        _externo.Setup(e => e.IniciarConGoogleAsync("id-token", consentimientos)).ReturnsAsync(new ResultadoLoginExterno(Usuario(), CuentaNueva: true));
        SesionQueEmite();

        var resultado = await Controlador(Http()).Google(Peticion(consentimientos));

        var creado = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(201, creado.StatusCode);
        Assert.Equal("jwt-de-acceso", Assert.IsType<UsuarioResponseDto>(creado.Value).Token);
    }

    [Theory]
    [InlineData(typeof(VinculacionRequeridaException))]
    [InlineData(typeof(ConsentimientoRequeridoException))]
    [InlineData(typeof(TokenExternoInvalidoException))]
    [InlineData(typeof(ProveedorExternoNoDisponibleException))]
    [InlineData(typeof(CuentaBloqueadaException))]
    public async Task RF_CA_03_UnRechazoNoAbreSesionNiFijaCookie(Type tipoDeError)
    {
        _externo.Setup(e => e.IniciarConGoogleAsync(It.IsAny<string>(), It.IsAny<ConsentimientosRegistroDto?>()))
            .ThrowsAsync((Exception)Activator.CreateInstance(tipoDeError)!);
        var http = Http();

        await Assert.ThrowsAsync(tipoDeError, () => Controlador(http).Google(Peticion()));

        _sesiones.Verify(s => s.IniciarAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        Assert.Empty(http.Response.Headers.SetCookie.ToString());
    }

    // ---------------- POST /google/vincular ----------------

    [Fact]
    public async Task RF_CA_03_VincularConLaClaveCorrectaIniciaSesionConCookie()
    {
        _externo.Setup(e => e.VincularGoogleAsync("id-token", "clave-correcta"))
            .ReturnsAsync(new ResultadoLoginExterno(Usuario(), CuentaNueva: false));
        SesionQueEmite();
        var http = Http();

        var resultado = await Controlador(http).VincularGoogle(new GoogleVincularRequest("id-token", "clave-correcta"));

        Assert.Equal("jwt-de-acceso", Assert.IsType<UsuarioResponseDto>(Assert.IsType<OkObjectResult>(resultado).Value).Token);
        Assert.Contains(OpcionesSesion.NombreCookie, http.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task RF_CA_19_VincularConLaClaveIncorrectaNoAbreSesion()
    {
        _externo.Setup(e => e.VincularGoogleAsync(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new CredencialesInvalidasException());
        var http = Http();

        await Assert.ThrowsAsync<CredencialesInvalidasException>(
            () => Controlador(http).VincularGoogle(new GoogleVincularRequest("id-token", "mala")));

        _sesiones.VerifyNoOtherCalls();
        Assert.Empty(http.Response.Headers.SetCookie.ToString());
    }

    // ---------------- Vínculos de la cuenta ----------------

    [Fact]
    public async Task RD_06_ListarVinculosUsaLaIdentidadDelToken()
    {
        var vinculos = new VinculosDto(true, [new VinculoDto("google", DateTime.UtcNow)]);
        _externo.Setup(e => e.ListarVinculosAsync(UsuarioId)).ReturnsAsync(vinculos);

        var resultado = await Controlador(Http(autenticado: true)).ListarVinculos();

        Assert.Same(vinculos, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    public async Task RD_06_DesvincularGoogleResponde204_YSoloParaLaCuentaDelToken()
    {
        _externo.Setup(e => e.DesvincularAsync(UsuarioId, "google")).ReturnsAsync(true);

        var resultado = await Controlador(Http(autenticado: true)).Desvincular("google");

        Assert.IsType<NoContentResult>(resultado);
        _externo.Verify(e => e.DesvincularAsync(It.Is<Guid>(g => g != UsuarioId), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RF_CA_03_DesvincularUnVinculoInexistenteResponde404()
    {
        _externo.Setup(e => e.DesvincularAsync(UsuarioId, "google")).ReturnsAsync(false);

        var resultado = await Controlador(Http(autenticado: true)).Desvincular("google");

        Assert.Equal(404, Assert.IsType<ProblemDetails>(Assert.IsType<NotFoundObjectResult>(resultado).Value).Status);
    }

    [Theory]
    [InlineData("facebook")]
    [InlineData("GOOGLE")]
    [InlineData("../../x")]
    public async Task RD_07_UnProveedorDistintoDeGoogleResponde404SinLlegarAlServicio(string proveedor)
    {
        var resultado = await Controlador(Http(autenticado: true)).Desvincular(proveedor);

        Assert.IsType<NotFoundObjectResult>(resultado);
        _externo.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RD_06_LosEndpointsDeVinculosConUnTokenSinIdentidadSeRechazanSinLlegarAlServicio()
    {
        var controlador = Controlador(Http(autenticado: false));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.ListarVinculos());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.Desvincular("google"));

        _externo.VerifyNoOtherCalls();
    }

    // ---------------- Atributos ----------------

    [Theory]
    [InlineData(nameof(AuthController.Google))]
    [InlineData(nameof(AuthController.VincularGoogle))]
    public void RD_06_LosEndpointsDeEntradaSonAnonimosPeroTienenSuPropioLimite(string metodo)
    {
        var accion = typeof(AuthController).GetMethod(metodo)!;

        Assert.NotNull(accion.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(LimitacionDePeticionesExtensions.Google, accion.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Theory]
    [InlineData(nameof(AuthController.ListarVinculos))]
    [InlineData(nameof(AuthController.Desvincular))]
    public void RD_06_LosEndpointsDeVinculosExigenUnUsuarioAutenticado(string metodo)
    {
        Assert.NotNull(typeof(AuthController).GetMethod(metodo)!.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void RD_06_LasRutasDeGoogleYVinculosEstanBajoApiAuth()
    {
        string RutaDe(string metodo) => typeof(AuthController).GetMethod(metodo)!
            .GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single().Template!;

        Assert.Equal("google", RutaDe(nameof(AuthController.Google)));
        Assert.Equal("google/vincular", RutaDe(nameof(AuthController.VincularGoogle)));
        Assert.Equal("vinculos", RutaDe(nameof(AuthController.ListarVinculos)));
        Assert.Equal("vinculos/{proveedor}", RutaDe(nameof(AuthController.Desvincular)));
    }
}
