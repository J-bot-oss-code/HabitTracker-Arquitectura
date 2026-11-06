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

/// <summary>Cookie del refresh token, cabecera anti-CSRF y endpoints de sesión (RF-CA-03, 12, 18, RD-06).</summary>
public class SesionesControllerTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private readonly Mock<IUsuarioService> _usuarios = new();
    private readonly Mock<ITokenBlacklist> _blacklist = new();
    private readonly Mock<IConsentimientoService> _consentimientos = new();
    private readonly Mock<IGestorSesiones> _sesiones = new();

    private static DefaultHttpContext Http(bool autenticado = true, string? cookie = null, string? csrf = null, string? bearer = null)
    {
        var claims = autenticado ? new[] { new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString()) } : [];
        var http = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, autenticado ? "prueba" : null)) };
        if (cookie is not null) http.Request.Headers.Cookie = $"{OpcionesSesion.NombreCookie}={cookie}";
        if (csrf is not null) http.Request.Headers[OpcionesSesion.CabeceraCsrf] = csrf;
        if (bearer is not null) http.Request.Headers.Authorization = $"Bearer {bearer}";
        http.Request.Headers.UserAgent = "Firefox en Windows";
        return http;
    }

    private AuthController Controlador(DefaultHttpContext http, OpcionesSesion? opciones = null) =>
        new(_usuarios.Object, _blacklist.Object, _consentimientos.Object, _sesiones.Object, opciones ?? OpcionesSesion.PorDefecto, Mock.Of<IServicioLoginExterno>())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };

    private static string SetCookie(DefaultHttpContext http) => http.Response.Headers.SetCookie.ToString().ToLowerInvariant();

    private static SesionEmitida Emitida(string token = "refresh-nuevo") => new(token, DateTime.UtcNow.AddDays(14));

    // ---------------- Cookie del login ----------------

    [Fact]
    public async Task RF_CA_03_ElLoginFijaLaCookieDelRefreshToken_HttpOnlySeguraYSoloParaApiAuth()
    {
        _usuarios.Setup(u => u.AutenticarUsuarioAsync(It.IsAny<LoginRequestDto>()))
            .ReturnsAsync(new UsuarioResponseDto(UsuarioId, "Persona", "persona@ejemplo.com", "Estandar", true, "jwt"));
        _sesiones.Setup(s => s.IniciarAsync(UsuarioId, "Firefox en Windows", It.IsAny<string?>())).ReturnsAsync(Emitida("refresh-del-login"));
        var http = Http(autenticado: false);

        var resultado = await Controlador(http).Login(new LoginRequestDto("persona@ejemplo.com", "ClaveSegura-Nexus-2026"));

        var cuerpo = Assert.IsType<UsuarioResponseDto>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("jwt", cuerpo.Token);   // el acceso sigue viajando en el cuerpo, como antes
        var cookie = SetCookie(http);
        Assert.Contains($"{OpcionesSesion.NombreCookie}=refresh-del-login", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
        Assert.Contains("expires=", cookie);
        Assert.DoesNotContain("refresh-del-login", System.Text.Json.JsonSerializer.Serialize(cuerpo));   // el cuerpo no lleva el refresh
    }

    [Fact]
    public async Task RF_CA_03_UnLoginFallidoNoAbreSesionNiFijaCookie()
    {
        _usuarios.Setup(u => u.AutenticarUsuarioAsync(It.IsAny<LoginRequestDto>())).ThrowsAsync(new CredencialesInvalidasException());
        var http = Http(autenticado: false);

        await Assert.ThrowsAsync<CredencialesInvalidasException>(
            () => Controlador(http).Login(new LoginRequestDto("persona@ejemplo.com", "mala")));

        _sesiones.Verify(s => s.IniciarAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
        Assert.Empty(SetCookie(http));
    }

    [Fact]
    public async Task RD_06_LaCookieRespetaSameSiteNoneYSegura_CuandoElFrontendEstaEnOtroDominio()
    {
        _usuarios.Setup(u => u.AutenticarUsuarioAsync(It.IsAny<LoginRequestDto>()))
            .ReturnsAsync(new UsuarioResponseDto(UsuarioId, "Persona", "persona@ejemplo.com", "Estandar", true, "jwt"));
        _sesiones.Setup(s => s.IniciarAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<string?>())).ReturnsAsync(Emitida());
        var http = Http(autenticado: false);

        await Controlador(http, new OpcionesSesion { CookieSameSite = "None" }).Login(new LoginRequestDto("persona@ejemplo.com", "x"));

        Assert.Contains("samesite=none", SetCookie(http));
        Assert.Contains("secure", SetCookie(http));
    }

    // ---------------- Refresh ----------------

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("true")]
    public async Task RD_06_RefreshSinLaCabeceraAntiCsrfSeRechaza_SinLlegarAlServicio(string? valor)
    {
        var http = Http(autenticado: false, cookie: "refresh-actual", csrf: valor);

        var resultado = await Controlador(http).Refresh();

        var respuesta = Assert.IsType<ObjectResult>(resultado);
        Assert.Equal(403, respuesta.StatusCode);
        Assert.Equal("csrf_requerido", Assert.IsType<ProblemDetails>(respuesta.Value).Extensions["codigo"]);
        _sesiones.VerifyNoOtherCalls();
        Assert.Empty(SetCookie(http));
    }

    [Fact]
    public async Task RF_CA_12_RefreshConLaCabeceraLeeLaCookie_RotaLaCookieYDevuelveElAccesoNuevo()
    {
        var usuario = new UsuarioResponseDto(UsuarioId, "Persona", "persona@ejemplo.com", "Estandar", true, "jwt-nuevo");
        _sesiones.Setup(s => s.RenovarAsync("refresh-actual")).ReturnsAsync(new SesionRenovada(usuario, "refresh-rotado", DateTime.UtcNow.AddDays(14)));
        _consentimientos.Setup(c => c.RequiereAceptarDocumentosAsync(UsuarioId)).ReturnsAsync(true);
        var http = Http(autenticado: false, cookie: "refresh-actual", csrf: "1");

        var resultado = await Controlador(http).Refresh();

        var cuerpo = Assert.IsType<UsuarioResponseDto>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal("jwt-nuevo", cuerpo.Token);
        Assert.True(cuerpo.RequiereAceptarDocumentos);
        var cookie = SetCookie(http);
        Assert.Contains($"{OpcionesSesion.NombreCookie}=refresh-rotado", cookie);   // la cookie se reemplaza por la nueva
        Assert.DoesNotContain("refresh-actual", cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("path=/api/auth", cookie);
    }

    [Fact]
    public async Task RF_CA_18_RefreshInvalidoBorraLaCookieYPropagaLaExcepcion()
    {
        _sesiones.Setup(s => s.RenovarAsync("refresh-viejo")).ThrowsAsync(new SesionInvalidaException());
        var http = Http(autenticado: false, cookie: "refresh-viejo", csrf: "1");

        await Assert.ThrowsAsync<SesionInvalidaException>(() => Controlador(http).Refresh());

        var cookie = SetCookie(http);
        Assert.Contains($"{OpcionesSesion.NombreCookie}=;", cookie);   // valor vacío y fecha pasada: el navegador la elimina
        Assert.Contains("path=/api/auth", cookie);
        Assert.Contains("httponly", cookie);
    }

    [Fact]
    public async Task RF_CA_18_RefreshSinCookieLlegaAlServicioComoNulo_ParaQueLoRechace()
    {
        _sesiones.Setup(s => s.RenovarAsync(null)).ThrowsAsync(new SesionInvalidaException());
        var http = Http(autenticado: false, csrf: "1");

        await Assert.ThrowsAsync<SesionInvalidaException>(() => Controlador(http).Refresh());

        _sesiones.Verify(s => s.RenovarAsync(null), Times.Once);
    }

    // ---------------- Logout ----------------

    [Fact]
    public async Task RF_CA_18_ElLogoutInvalidaElAccesoRevocaElRefreshDeSuCookieYLaBorra()
    {
        var http = Http(cookie: "refresh-actual", bearer: "jwt-actual");

        var resultado = await Controlador(http).Logout();

        Assert.IsType<OkObjectResult>(resultado);
        _blacklist.Verify(b => b.InvalidarToken("jwt-actual"), Times.Once);
        _sesiones.Verify(s => s.CerrarAsync(UsuarioId, "refresh-actual"), Times.Once);
        Assert.Contains($"{OpcionesSesion.NombreCookie}=;", SetCookie(http));
    }

    [Fact]
    public async Task RD_06_ElLogoutUsaLaIdentidadDelToken_NuncaUnaDeLaPeticion()
    {
        var otro = Guid.NewGuid();
        await Controlador(Http(cookie: "refresh-actual", bearer: "jwt")).Logout();

        _sesiones.Verify(s => s.CerrarAsync(otro, It.IsAny<string?>()), Times.Never);
        _sesiones.Verify(s => s.CerrarAsync(UsuarioId, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task RF_CA_18_LogoutTodasRevocaTodoYBorraLaCookie()
    {
        var http = Http(cookie: "refresh-actual");

        var resultado = await Controlador(http).LogoutTodas();

        Assert.IsType<OkObjectResult>(resultado);
        _sesiones.Verify(s => s.RevocarTodasAsync(UsuarioId, It.IsAny<string>()), Times.Once);
        Assert.Contains($"{OpcionesSesion.NombreCookie}=;", SetCookie(http));
    }

    // ---------------- Lista y revocación de sesiones ----------------

    [Fact]
    public async Task RF_CA_18_ListarSesionesDevuelveLasDelUsuarioDelToken_ConLaActualMarcada()
    {
        var lista = new List<SesionActivaDto> { new(Guid.NewGuid(), "Firefox", DateTime.UtcNow, DateTime.UtcNow, true) };
        _sesiones.Setup(s => s.ListarAsync(UsuarioId, "refresh-actual")).ReturnsAsync(lista);

        var resultado = await Controlador(Http(cookie: "refresh-actual")).ListarSesiones();

        Assert.Same(lista, Assert.IsAssignableFrom<IReadOnlyList<SesionActivaDto>>(Assert.IsType<OkObjectResult>(resultado).Value));
    }

    [Fact]
    public async Task RF_CA_18_RevocarUnaSesionPropiaResponde204()
    {
        var id = Guid.NewGuid();
        _sesiones.Setup(s => s.RevocarAsync(UsuarioId, id)).ReturnsAsync(true);

        Assert.IsType<NoContentResult>(await Controlador(Http()).RevocarSesion(id));
    }

    [Fact]
    public async Task RD_06_RevocarUnaSesionAjenaOInexistenteResponde404()
    {
        var id = Guid.NewGuid();
        _sesiones.Setup(s => s.RevocarAsync(UsuarioId, id)).ReturnsAsync(false);

        var resultado = await Controlador(Http()).RevocarSesion(id);

        Assert.Equal(404, Assert.IsType<ProblemDetails>(Assert.IsType<NotFoundObjectResult>(resultado).Value).Status);
    }

    [Fact]
    public async Task RD_06_LosEndpointsDeSesionConUnTokenSinIdentidadSeRechazanSinLlegarAlServicio()
    {
        var sinIdentidad = Http(autenticado: false);
        var controlador = Controlador(sinIdentidad);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.Logout());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.LogoutTodas());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.ListarSesiones());
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.RevocarSesion(Guid.NewGuid()));

        _sesiones.VerifyNoOtherCalls();
    }

    // ---------------- Atributos ----------------

    [Theory]
    [InlineData(nameof(AuthController.LogoutTodas))]
    [InlineData(nameof(AuthController.ListarSesiones))]
    [InlineData(nameof(AuthController.RevocarSesion))]
    [InlineData(nameof(AuthController.Logout))]
    public void RD_06_LosEndpointsDeSesionExigenUnUsuarioAutenticado(string metodo)
    {
        Assert.NotNull(typeof(AuthController).GetMethod(metodo)!.GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public void RD_06_RefreshEsAnonimoPeroTieneSuPropioLimiteDePeticiones()
    {
        var metodo = typeof(AuthController).GetMethod(nameof(AuthController.Refresh))!;

        Assert.NotNull(metodo.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(LimitacionDePeticionesExtensions.Refresh, metodo.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    public void RD_06_LasRutasDeSesionEstanBajoApiAuth_DondeLaCookieSiViaja()
    {
        string RutaDe(string metodo) => typeof(AuthController).GetMethod(metodo)!.GetCustomAttributes<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().Single().Template!;

        Assert.Equal("refresh", RutaDe(nameof(AuthController.Refresh)));
        Assert.Equal("logout-todas", RutaDe(nameof(AuthController.LogoutTodas)));
        Assert.Equal("sesiones", RutaDe(nameof(AuthController.ListarSesiones)));
        Assert.Equal("sesiones/{id:guid}", RutaDe(nameof(AuthController.RevocarSesion)));
        Assert.StartsWith(OpcionesSesion.RutaCookie.TrimStart('/'), typeof(AuthController).GetCustomAttribute<RouteAttribute>()!.Template);
    }
}
