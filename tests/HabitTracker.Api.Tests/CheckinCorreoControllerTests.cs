using System.Reflection;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Notificaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Controllers;
using WebApi.Extensions;

namespace HabitTracker.Api.Tests;

/// <summary>El enlace «Ya lo hice» del correo, sin levantar la API (RD-12): el GET no tiene efectos y todo lo escrito se codifica.</summary>
public class CheckinCorreoControllerTests
{
    private sealed class ServicioFalso : ICheckinCorreoService
    {
        public string NombreDelHabito { get; set; } = "Tomar agua";
        public bool TokenValido { get; set; } = true;
        public int Vistas { get; private set; }
        public int Ejecuciones { get; private set; }
        public string? UltimoToken { get; private set; }

        public Task<InfoCheckin> VerAsync(string token, CancellationToken cancellationToken = default)
        {
            Vistas++;
            return TokenValido ? Task.FromResult(new InfoCheckin(NombreDelHabito, new DateOnly(2026, 11, 13))) : throw new EnlaceNoValidoException();
        }

        public Task EjecutarAsync(string token, CancellationToken cancellationToken = default)
        {
            Ejecuciones++;
            UltimoToken = token;
            return TokenValido ? Task.CompletedTask : throw new EnlaceNoValidoException();
        }
    }

    private static (CheckinCorreoController Controlador, ServicioFalso Servicio, DefaultHttpContext Http) Crear(Action<ServicioFalso>? configurar = null)
    {
        var servicio = new ServicioFalso();
        configurar?.Invoke(servicio);
        var http = new DefaultHttpContext();
        return (new CheckinCorreoController(servicio) { ControllerContext = new ControllerContext { HttpContext = http } }, servicio, http);
    }

    private static string Html(IActionResult resultado) => Assert.IsType<ContentResult>(resultado).Content!;

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Get_MuestraLaPaginaSinRegistrarNada()
    {
        var (c, servicio, _) = Crear();

        var resultado = await c.Ver("token-de-prueba", CancellationToken.None);

        Assert.Equal(200, Assert.IsType<ContentResult>(resultado).StatusCode);
        Assert.Contains("Tomar agua", Html(resultado));
        Assert.Contains("method=\"post\"", Html(resultado));
        Assert.Equal(1, servicio.Vistas);
        Assert.Equal(0, servicio.Ejecuciones); // el GET lo abren los antivirus y los clientes de correo: sin efectos
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    [Trait("Requirement", "RD-08")]
    public async Task RD_07_Get_ElNombreDelHabitoSeCodifica_NoHayXss()
    {
        var (c, _, _) = Crear(s => s.NombreDelHabito = "<script>alert('x')</script>");

        var html = Html(await c.Ver("t", CancellationToken.None));

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("basura")]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_Get_EnlaceNoValido_Es400_ConUnMensajeGenerico(string? token)
    {
        var (c, _, _) = Crear(s => s.TokenValido = false);

        var resultado = await c.Ver(token, CancellationToken.None);

        Assert.Equal(400, Assert.IsType<ContentResult>(resultado).StatusCode);
        Assert.Contains("ya no sirve", Html(resultado));
        Assert.DoesNotContain("excepci", Html(resultado), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Post_RegistraConElToken_YResponde200()
    {
        var (c, servicio, _) = Crear();

        var resultado = await c.Registrar("el-token", CancellationToken.None);

        Assert.Equal(200, Assert.IsType<ContentResult>(resultado).StatusCode);
        Assert.Equal(1, servicio.Ejecuciones);
        Assert.Equal("el-token", servicio.UltimoToken);
        Assert.Contains("¡Listo!", Html(resultado));
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_Post_EnlaceNoValido_Es400()
    {
        var (c, _, _) = Crear(s => s.TokenValido = false);

        var resultado = await c.Registrar("caducado", CancellationToken.None);

        Assert.Equal(400, Assert.IsType<ContentResult>(resultado).StatusCode);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    [Trait("Requirement", "RD-08")]
    public async Task RD_06_LasPaginas_LlevanCspNoReferrerYNoSniff()
    {
        var (c, _, http) = Crear();

        await c.Ver("t", CancellationToken.None);

        var csp = http.Response.Headers.ContentSecurityPolicy.ToString();
        Assert.Contains("default-src 'none'", csp);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("form-action 'self'", csp);
        Assert.DoesNotContain("script-src", csp); // sin scripts: ni siquiera se permiten
        Assert.Equal("no-referrer", http.Response.Headers["Referrer-Policy"].ToString());
        Assert.Equal("nosniff", http.Response.Headers.XContentTypeOptions.ToString());
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_ElControlador_EsAnonimo_PeroConLimiteDePeticionesYSinCache()
    {
        var tipo = typeof(CheckinCorreoController);

        Assert.NotNull(tipo.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(LimitacionDePeticionesExtensions.Checkin, tipo.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        Assert.True(tipo.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }
}
