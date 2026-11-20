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

/// <summary>Baja en un clic de los recordatorios, sin levantar la API (RD-12).</summary>
public class BajaRecordatoriosControllerTests
{
    private sealed class ServicioFalso : IRecordatoriosService
    {
        public bool TokenValido { get; set; } = true;
        public int Bajas { get; private set; }
        public Task<int> EjecutarAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task DarDeBajaAsync(string token, CancellationToken cancellationToken = default)
        {
            Bajas++;
            return TokenValido ? Task.CompletedTask : throw new EnlaceNoValidoException();
        }
    }

    private static (BajaRecordatoriosController Controlador, ServicioFalso Servicio, DefaultHttpContext Http) Crear()
    {
        var servicio = new ServicioFalso();
        var http = new DefaultHttpContext();
        return (new BajaRecordatoriosController(servicio) { ControllerContext = new ControllerContext { HttpContext = http } }, servicio, http);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public void RF_NEG_07_Get_SoloMuestraElBoton_NoDaDeBaja()
    {
        var (c, servicio, http) = Crear();

        var resultado = Assert.IsType<ContentResult>(c.Ver("token-de-prueba"));

        Assert.Equal(200, resultado.StatusCode);
        Assert.Contains("method=\"post\"", resultado.Content);
        Assert.Equal(0, servicio.Bajas); // los antivirus abren los enlaces solos: el GET no tiene efectos
        Assert.Contains("default-src 'none'", http.Response.Headers.ContentSecurityPolicy.ToString());
        Assert.Equal("no-referrer", http.Response.Headers["Referrer-Policy"].ToString());
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Get_ElTokenSeCodificaEnLaPagina()
    {
        var (c, _, _) = Crear();

        var html = Assert.IsType<ContentResult>(c.Ver("\"><script>alert(1)</script>")).Content!;

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-07")]
    public async Task RF_NEG_07_Post_DaDeBaja()
    {
        var (c, servicio, _) = Crear();

        var resultado = Assert.IsType<ContentResult>(await c.DarDeBaja("token", CancellationToken.None));

        Assert.Equal(200, resultado.StatusCode);
        Assert.Equal(1, servicio.Bajas);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_Post_ConUnEnlaceNoValido_Responde400ConUnMensajeGenerico()
    {
        var (c, servicio, _) = Crear();
        servicio.TokenValido = false;

        var resultado = Assert.IsType<ContentResult>(await c.DarDeBaja("malo", CancellationToken.None));

        Assert.Equal(400, resultado.StatusCode);
        Assert.Contains("ya no sirve", resultado.Content);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_EsAnonimoPeroConLimiteDePeticionesPorIp()
    {
        var tipo = typeof(BajaRecordatoriosController);

        Assert.NotNull(tipo.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Equal(LimitacionDePeticionesExtensions.Checkin, tipo.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }
}
