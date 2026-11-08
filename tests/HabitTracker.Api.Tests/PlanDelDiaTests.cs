using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Controllers;
using WebApi.Middlewares;

namespace HabitTracker.Api.Tests;

/// <summary>Pruebas del endpoint de plan del día (API externa de clima) y de cómo se rechazan sus fallos.</summary>
public class PlanDelDiaTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_PlanDelDia_ExigeSesionYRolEstandar()
    {
        var authorize = typeof(PlanDelDiaController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Estandar", authorize.Roles);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_PlanDelDia_LaIdentidadSaleDeLosClaimsYLaCiudadSePasaAlServicio()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ConSub(UsuarioId.ToString()));

        var resultado = await controlador.Obtener("Santo Domingo", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        Assert.IsType<PlanDelDiaDto>(ok.Value);
        Assert.Equal(UsuarioId, servicio.UltimoUsuario);
        Assert.Equal("Santo Domingo", servicio.UltimaCiudad);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-es-guid")]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_PlanDelDia_SinSubValido_NoAutorizado(string? sub)
    {
        var controlador = Crear(new ServicioFalso(), ConSub(sub));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.Obtener("Santo Domingo", CancellationToken.None));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_ClimaNoDisponible_Responde503ConMensajeGenericoYProblemJson()
    {
        var (status, tipo, cuerpo) = await EjecutarAsync(new ServicioClimaNoDisponibleException());

        Assert.Equal(503, status);
        Assert.Equal("application/problem+json", tipo);
        Assert.Contains("clima", cuerpo.GetProperty("detail").GetString());
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_ClimaNoDisponible_NoFiltraDetallesInternos()
    {
        var (_, _, cuerpo) = await EjecutarAsync(new ServicioClimaNoDisponibleException());

        var texto = cuerpo.GetRawText();
        Assert.DoesNotContain("appid", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("openweathermap", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", texto);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_CiudadNoEncontrada_Responde404()
    {
        var (status, _, cuerpo) = await EjecutarAsync(new CiudadNoEncontradaException());

        Assert.Equal(404, status);
        Assert.Equal("No se encontró la ciudad indicada.", cuerpo.GetProperty("detail").GetString());
    }

    private static async Task<(int Status, string? Tipo, JsonElement Cuerpo)> EjecutarAsync(Exception excepcion)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(_ => throw excepcion, NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        var cuerpo = await JsonDocument.ParseAsync(contexto.Response.Body);
        return (contexto.Response.StatusCode, contexto.Response.ContentType, cuerpo.RootElement.Clone());
    }

    private static ClaimsPrincipal ConSub(string? sub)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, "Estandar") };
        if (sub is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static PlanDelDiaController Crear(IPlanDelDiaService servicio, ClaimsPrincipal usuario) =>
        new(servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = usuario } }
        };

    private sealed class ServicioFalso : IPlanDelDiaService
    {
        public Guid? UltimoUsuario { get; private set; }
        public string? UltimaCiudad { get; private set; }

        public Task<PlanDelDiaDto> ObtenerAsync(Guid usuarioId, string ciudad, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            UltimaCiudad = ciudad;
            return Task.FromResult(new PlanDelDiaDto(ciudad, new ClimaDto(20, false, "cielo claro"), 0, []));
        }
    }
}
