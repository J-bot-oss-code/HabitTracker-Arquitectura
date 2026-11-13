using AccessControl.Application.Service;
using System.Text.RegularExpressions;
using AccessControl.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WebApi.Controllers;
using WebApi.Extensions;

namespace AccessControl.Application.Tests;

/// <summary>Configuración de seguridad del host: página de activación, CORS y límites de peticiones.</summary>
public class HostSeguridadTests
{
    private static IConfiguration Config(params (string Clave, string? Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.ToDictionary(v => v.Clave, v => v.Valor))
            .Build();

    // ---------------- Página de activación: un GET nunca cambia el estado (RF-CA-15) ----------------

    private static (AuthController Controlador, Mock<IUsuarioService> Servicio, DefaultHttpContext Http) CrearControlador()
    {
        var servicio = new Mock<IUsuarioService>();
        var http = new DefaultHttpContext();
        var controlador = new AuthController(servicio.Object, Mock.Of<ITokenBlacklist>(), Mock.Of<IConsentimientoService>(), Mock.Of<IGestorSesiones>(), OpcionesSesion.PorDefecto, Mock.Of<IServicioLoginExterno>())
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        return (controlador, servicio, http);
    }

    [Fact]
    public void RF_CA_15_ElGetDelEnlaceSoloEntregaUnaPagina_NoActivaNiGastaIntentos()
    {
        var (controlador, servicio, _) = CrearControlador();

        var resultado = controlador.ActivateGet();

        Assert.Equal("text/html", resultado.ContentType);
        Assert.Contains("fetch('/api/auth/activate'", resultado.Content);
        // Un escáner de correos abre el enlace con GET: el servicio de activación no debe ejecutarse.
        servicio.VerifyNoOtherCalls();
    }

    [Fact]
    public void RD_08_LaPaginaDeActivacion_NoSeGuardaEnCacheNiFiltraElRefererYPermiteSoloSuScript()
    {
        var (controlador, _, http) = CrearControlador();

        var resultado = controlador.ActivateGet();

        Assert.Equal("no-store", http.Response.Headers.CacheControl.ToString());
        Assert.Equal("no-referrer", http.Response.Headers["Referrer-Policy"].ToString());

        var csp = http.Response.Headers.ContentSecurityPolicy.ToString();
        var nonce = Regex.Match(csp, "script-src 'nonce-([^']+)'").Groups[1].Value;
        Assert.NotEmpty(nonce);
        Assert.Contains($"<script nonce=\"{nonce}\">", resultado.Content);
        Assert.Contains("default-src 'none'", csp);
        Assert.DoesNotContain("unsafe-inline", csp);
    }

    [Fact]
    public void RD_08_CadaRespuestaDeLaPaginaUsaUnNonceDistinto()
    {
        var (controlador, _, http) = CrearControlador();
        controlador.ActivateGet();
        var primero = http.Response.Headers.ContentSecurityPolicy.ToString();

        var (otro, _, otroHttp) = CrearControlador();
        otro.ActivateGet();

        Assert.NotEqual(primero, otroHttp.Response.Headers.ContentSecurityPolicy.ToString());
    }

    // ---------------- CORS: orígenes exactos, nunca comodines (RD-06, RD-07) ----------------

    [Fact]
    public void RD_06_SinOrigenesConfigurados_NoSePermiteNingunoYNoSeRegistraLaPolitica()
    {
        var cfg = Config();

        Assert.Empty(CorsExtensions.LeerOrigenesPermitidos(cfg));
        var servicios = new ServiceCollection().AddCorsDelFrontend(cfg);
        Assert.DoesNotContain(servicios, d => d.ServiceType.Name.Contains("CorsOptions", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RF_DOC_03_LaPoliticaDeCors_ExponeContentDisposition_ParaElNombreDeLaDescarga()
    {
        var cfg = Config(("Cors:OrigenesPermitidos:0", "http://localhost:5174"));
        await using var proveedor = new ServiceCollection().AddLogging().AddCorsDelFrontend(cfg).BuildServiceProvider();

        var politica = await proveedor.GetRequiredService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider>()
            .GetPolicyAsync(new Microsoft.AspNetCore.Http.DefaultHttpContext(), CorsExtensions.PoliticaFrontend);

        Assert.Contains("Content-Disposition", politica!.ExposedHeaders);
        Assert.False(politica.AllowAnyOrigin); // sigue siendo una lista exacta de orígenes
    }

    [Fact]
    public void RD_07_LosOrigenesSeNormalizan_SinBarraFinalNiDuplicadosNiVacios()
    {
        var cfg = Config(
            ("Cors:OrigenesPermitidos:0", "http://localhost:5173/"),
            ("Cors:OrigenesPermitidos:1", " http://localhost:5173 "),
            ("Cors:OrigenesPermitidos:2", ""),
            ("Cors:OrigenesPermitidos:3", "https://app.ejemplo.com:8443"));

        Assert.Equal(["http://localhost:5173", "https://app.ejemplo.com:8443"], CorsExtensions.LeerOrigenesPermitidos(cfg));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("localhost:5173")]
    [InlineData("http://localhost:5173/ruta")]
    [InlineData("http://localhost:5173?x=1")]
    [InlineData("ftp://localhost")]
    [InlineData("javascript:alert(1)")]
    public void RD_07_UnOrigenInvalidoOUnComodin_DetieneElArranque(string origen)
    {
        var cfg = Config(("Cors:OrigenesPermitidos:0", origen));

        var error = Assert.Throws<InvalidOperationException>(() => CorsExtensions.LeerOrigenesPermitidos(cfg));

        Assert.Contains("Cors:OrigenesPermitidos", error.Message);
    }

    // ---------------- Límite de peticiones configurable y validado (RD-06, RD-07) ----------------

    [Fact]
    public void RD_06_LaConfiguracionPorDefectoDeLosLimitesEsValida()
    {
        var servicios = new ServiceCollection().AddLimitacionDePeticiones(Config());

        var opciones = servicios.BuildServiceProvider().GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        Assert.Equal(StatusCodes.Status429TooManyRequests, opciones.RejectionStatusCode);
    }

    [Theory]
    [InlineData("LoginPorMinuto", "0")]
    [InlineData("RegistroPorMinuto", "-5")]
    [InlineData("RecuperacionPorMinuto", "0")]
    [InlineData("ActivacionPorMinuto", "0")]
    [InlineData("RefreshPorMinuto", "0")]
    [InlineData("GooglePorMinuto", "-1")]
    [InlineData("PrivacidadPorMinuto", "0")]
    public void RD_07_UnTopeDeCeroOMenos_DetieneElArranqueEnLugarDeBloquearElEndpoint(string clave, string valor)
    {
        var servicios = new ServiceCollection().AddLimitacionDePeticiones(Config(($"RateLimit:{clave}", valor)));
        var proveedor = servicios.BuildServiceProvider();

        var error = Assert.Throws<InvalidOperationException>(
            () => proveedor.GetRequiredService<IOptions<RateLimiterOptions>>().Value);

        Assert.Contains(clave, error.Message);
    }
}
