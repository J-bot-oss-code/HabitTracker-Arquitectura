using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Planes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Controllers;
using WebApi.Extensions;
using WebApi.Middlewares;

namespace HabitTracker.Api.Tests;

/// <summary>Controller del catálogo de planes sin levantar la API (RD-12).</summary>
public class PlansControllerTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static MethodInfo Metodo(string nombre) => typeof(PlansController).GetMethod(nombre)!;

    [Theory]
    [InlineData(nameof(PlansController.Listar))]
    [InlineData(nameof(PlansController.Obtener))]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_02_CatalogoYDetalle_SonPublicosConCacheYLimitePorIp(string nombre)
    {
        var metodo = Metodo(nombre);

        Assert.NotNull(metodo.GetCustomAttribute<AllowAnonymousAttribute>());
        var cache = metodo.GetCustomAttribute<ResponseCacheAttribute>();
        Assert.Equal(300, cache!.Duration);
        Assert.Equal(ResponseCacheLocation.Any, cache.Location);
        Assert.Equal(LimitacionDePeticionesExtensions.Catalogo, metodo.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_06_Recomendados_ExigenRolEstandarYNoSeGuardanEnCache()
    {
        var metodo = Metodo(nameof(PlansController.Recomendados));

        Assert.Equal("Estandar", metodo.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Null(metodo.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.True(metodo.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_Recomendados_LaIdentidadSaleDelToken()
    {
        var servicio = new PlanesFalsos();
        var controlador = new PlansController(servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Usuario(UsuarioId.ToString()) } },
        };

        var resultado = await controlador.Recomendados(CancellationToken.None);

        Assert.IsType<RecomendacionesDto>(Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(UsuarioId, servicio.UltimoUsuario);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RF_NEG_06_Recomendados_TokenSinIdentidadValida_SeRechaza()
    {
        var controlador = new PlansController(new PlanesFalsos())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Usuario("no-es-un-guid") } },
        };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.Recomendados(CancellationToken.None));
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Listar_PasaLosFiltrosAlServicio()
    {
        var servicio = new PlanesFalsos();
        var controlador = new PlansController(servicio);

        await controlador.Listar(AreaPlan.Descanso, NivelPlan.Basico, HabitTracker.Domain.Perfil.PerfilLaboral.Remoto, CancellationToken.None);

        Assert.Equal(new FiltrosPlanes(AreaPlan.Descanso, NivelPlan.Basico, HabitTracker.Domain.Perfil.PerfilLaboral.Remoto), servicio.UltimosFiltros);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_PlanNoEncontrado_Responde404ConProblemDetailsYCodigo()
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        var excepcion = new RecursoNoEncontradoException(RecursoNoEncontradoException.PlanNoEncontrado, "No encontramos ese plan.");
        var middleware = new GlobalExceptionMiddleware(_ => throw excepcion, NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        var cuerpo = (await JsonDocument.ParseAsync(contexto.Response.Body)).RootElement;
        Assert.Equal(404, contexto.Response.StatusCode);
        Assert.Equal("plan_no_encontrado", cuerpo.GetProperty("codigo").GetString());
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_ErrorEnUnEndpointConCachePublica_NoSeGuardaEnCache(int status)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        // Lo que deja [ResponseCache] antes de que la acción lance.
        contexto.Response.Headers.CacheControl = "public,max-age=300";
        Exception excepcion = status == 404
            ? new RecursoNoEncontradoException(RecursoNoEncontradoException.PlanNoEncontrado, "No encontramos ese plan.")
            : new TimeoutException("La base de datos no respondió.");
        var middleware = new GlobalExceptionMiddleware(_ => throw excepcion, NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        Assert.Equal(status, contexto.Response.StatusCode);
        Assert.Equal("no-store", contexto.Response.Headers.CacheControl.ToString());
    }

    private static ClaimsPrincipal Usuario(string sub) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, sub), new Claim(ClaimTypes.Role, "Estandar")], "Bearer"));

    private sealed class PlanesFalsos : IPlanesService
    {
        public Guid UltimoUsuario { get; private set; }
        public FiltrosPlanes? UltimosFiltros { get; private set; }

        public Task<IReadOnlyList<PlanResumenDto>> ListarAsync(FiltrosPlanes filtros, CancellationToken cancellationToken = default)
        {
            UltimosFiltros = filtros;
            return Task.FromResult<IReadOnlyList<PlanResumenDto>>([]);
        }

        public Task<PlanDetalleDto> ObtenerAsync(string slug, CancellationToken cancellationToken = default) =>
            throw new RecursoNoEncontradoException(RecursoNoEncontradoException.PlanNoEncontrado, "No encontramos ese plan.");

        public Task<RecomendacionesDto> RecomendarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            return Task.FromResult(new RecomendacionesDto([], [], false));
        }
    }
}
