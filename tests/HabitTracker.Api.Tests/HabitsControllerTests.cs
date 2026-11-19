using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Excepciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Controllers;
using WebApi.Middlewares;

namespace HabitTracker.Api.Tests;

/// <summary>
/// Pruebas del controller de hábitos sin levantar la API (RD-12): la identidad sale del token, el controller solo delega
/// en <see cref="IHabitoService"/> y los rechazos del módulo llegan al cliente como ProblemDetails con un código estable.
/// </summary>
public class HabitsControllerTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_06_HabitsController_ExigeSesionYRolEstandar()
    {
        var authorize = typeof(HabitsController).GetCustomAttribute<AuthorizeAttribute>();

        Assert.NotNull(authorize);
        Assert.Equal("Estandar", authorize.Roles);
    }

    [Fact]
    [Trait("Requirement", "RD-01")]
    public void RD_01_HabitsController_DependeDeLaInterfazDelServicio()
    {
        var parametros = typeof(HabitsController).GetConstructors().Single().GetParameters();

        Assert.Equal(typeof(IHabitoService), Assert.Single(parametros).ParameterType);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_02_Obtener_UsaLaIdentidadDelTokenYDevuelveElHabito()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ConSub(UsuarioId.ToString()));
        var habitoId = Guid.NewGuid();

        var resultado = await controlador.Obtener(habitoId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(resultado);
        var dto = Assert.IsType<HabitoDto>(ok.Value);
        Assert.Equal(habitoId, dto.Id);
        Assert.Equal(UsuarioId, servicio.UltimoUsuario);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_Crear_SinMeta_PasaMetaNulaYResponde201()
    {
        var servicio = new ServicioFalso();
        var controlador = Crear(servicio, ConSub(UsuarioId.ToString()));

        var resultado = await controlador.Crear(new CrearHabitoRequest("Beber agua", Periodicidad.Diaria, 8, "vasos"), CancellationToken.None);

        var creado = Assert.IsType<CreatedAtActionResult>(resultado);
        Assert.Equal(nameof(HabitsController.Obtener), creado.ActionName);
        Assert.NotNull(servicio.UltimoComando);
        Assert.Null(servicio.UltimoComando.MetaId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("no-es-guid")]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_Obtener_SinSubValido_NoAutorizado(string? sub)
    {
        var controlador = Crear(new ServicioFalso(), ConSub(sub));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => controlador.Obtener(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_HabitoNoEncontrado_Responde404ConProblemDetailsYCodigo()
    {
        var (status, tipo, cuerpo) = await EjecutarAsync(
            new RecursoNoEncontradoException(RecursoNoEncontradoException.HabitoNoEncontrado, "El hábito no existe."));

        Assert.Equal(404, status);
        Assert.Equal("application/problem+json", tipo);
        // El frontend toma un 404 sin cuerpo como "endpoint inexistente": este debe traer ProblemDetails.
        Assert.Equal("No encontrado.", cuerpo.GetProperty("title").GetString());
        Assert.Equal("habito_no_encontrado", cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    [Trait("Requirement", "RD-08")]
    public async Task RD_07_FechaFutura_Responde400ConCodigo()
    {
        var (status, _, cuerpo) = await EjecutarAsync(
            new ReglaNegocioException(ReglaNegocioException.FechaFutura, "La fecha del avance no puede ser futura."));

        Assert.Equal(400, status);
        Assert.Equal("fecha_futura", cuerpo.GetProperty("codigo").GetString());
        Assert.DoesNotContain("Exception", cuerpo.GetRawText());
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    [Trait("Requirement", "RD-08")]
    public async Task RF_NEG_04_TransicionProhibida_Sigue400GenericoSinCodigo()
    {
        var (status, _, cuerpo) = await EjecutarAsync(new InvalidOperationException("La transición de Planificado a Completado está prohibida."));

        Assert.Equal(400, status);
        Assert.False(cuerpo.TryGetProperty("codigo", out _));
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
        var claims = new List<Claim> { new(ClaimTypes.Role, "Estandar"), new(ClaimTypes.Email, "persona@ejemplo.com") };
        if (sub is not null)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, sub));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static HabitsController Crear(IHabitoService servicio, ClaimsPrincipal usuario) =>
        new(servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = usuario } }
        };

    private sealed class ServicioFalso : IHabitoService
    {
        public Guid? UltimoUsuario { get; private set; }
        public CrearHabitoCommand? UltimoComando { get; private set; }

        public Task<Guid> CrearHabitoAsync(CrearHabitoCommand command, Guid usuarioActualId, CancellationToken cancellationToken = default)
        {
            UltimoComando = command;
            UltimoUsuario = usuarioActualId;
            return Task.FromResult(Guid.NewGuid());
        }

        public Task<HabitoDto> ObtenerHabitoAsync(Guid habitoId, Guid usuarioActualId, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioActualId;
            return Task.FromResult(new HabitoDto(habitoId, "Leer", Periodicidad.Diaria, 10, "paginas", EstadoHabito.Activo,
                false, DateTime.UtcNow, []));
        }

        public Task<IReadOnlyList<HabitoDto>> ConsultarHabitosAsync(Guid usuarioActualId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<HabitoDto>>([]);

        public Task CambiarEstadoAsync(Guid habitoId, EstadoHabito nuevoEstado, Guid usuarioActualId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task RegistrarAvanceAsync(Guid habitoId, decimal cantidad, DateTime fechaUtc, Guid usuarioActualId, bool esVersionMinima = false, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReconfigurarHabitoAsync(Guid habitoId, Periodicidad periodicidad, decimal cantidadObjetivo, string unidad,
            Guid usuarioActualId, string correoSolicitante, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<int> EliminarDatosDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
    }
}
