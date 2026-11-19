using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Planes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Controllers;
using WebApi.Middlewares;

namespace HabitTracker.Api.Tests;

/// <summary>Controllers de Mis planes, Hoy y el resumen (Fase 4) sin levantar la API (RD-12).</summary>
public class MisPlanesControllersTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly Guid PlanId = Guid.NewGuid();

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    // ---------- Autorización ----------

    [Theory]
    [InlineData(typeof(MyPlansController))]
    [InlineData(typeof(TodayController))]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_06_ControllersDeMisPlanes_ExigenRolEstandarYNingunaAccionEsAnonima(Type controller)
    {
        Assert.Equal("Estandar", controller.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        foreach (var accion in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            Assert.Null(accion.GetCustomAttribute<AllowAnonymousAttribute>());
    }

    [Theory]
    [InlineData(typeof(MyPlansController))]
    [InlineData(typeof(TodayController))]
    [Trait("Requirement", "RD-06")]
    public void RD_06_DatosPersonales_NuncaSeGuardanEnCache(Type controller)
    {
        var cache = controller.GetCustomAttribute<ResponseCacheAttribute>();

        Assert.NotNull(cache);
        Assert.True(cache.NoStore);
    }

    // ---------- La identidad sale del token ----------

    private sealed class PlanesFalsos : IPlanUsuarioService
    {
        public Guid UltimoUsuario { get; private set; }
        public InscribirseCommand? Inscripcion { get; private set; }
        public CambiarEstadoPlanCommand? Transicion { get; private set; }
        public AjustarPlanCommand? Ajuste { get; private set; }
        public ConfigurarRecordatorioCommand? Recordatorio { get; private set; }

        private static PlanUsuarioDto Dto() => new(
            PlanId, "plan", "Plan", OrigenPlan.Catalogo, EstadoPlanUsuario.Activo, null, 4, 4, 0, DateTime.UtcNow, DateTime.UtcNow, null,
            null, false, [], null!, null, []);

        public Task<PlanUsuarioDto> InscribirseAsync(Guid usuarioId, InscribirseCommand command, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            Inscripcion = command;
            return Task.FromResult(Dto());
        }

        public Task<IReadOnlyList<PlanUsuarioDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            return Task.FromResult<IReadOnlyList<PlanUsuarioDto>>([]);
        }

        public Task<PlanUsuarioDto> ObtenerAsync(Guid usuarioId, Guid planId, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            return Task.FromResult(Dto());
        }

        public Task<PlanUsuarioDto> CambiarEstadoAsync(Guid usuarioId, Guid planId, CambiarEstadoPlanCommand command, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            Transicion = command;
            return Task.FromResult(Dto());
        }

        public Task<PlanUsuarioDto> AjustarAsync(Guid usuarioId, Guid planId, AjustarPlanCommand command, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            Ajuste = command;
            return Task.FromResult(Dto());
        }

        public Task<PlanUsuarioDto> ConfigurarRecordatorioAsync(Guid usuarioId, Guid planId, ConfigurarRecordatorioCommand command, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            Recordatorio = command;
            return Task.FromResult(Dto());
        }
    }

    private static MyPlansController Controlador(PlanesFalsos servicio, string? sub = null) => new(servicio)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, sub ?? UsuarioId.ToString()), new Claim(ClaimTypes.Role, "Estandar")], "Bearer")),
            },
        },
    };

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_CadaOperacion_UsaLaIdentidadDelTokenYNuncaLaDelCuerpo()
    {
        var servicio = new PlanesFalsos();
        var c = Controlador(servicio);

        var creado = Assert.IsType<CreatedResult>(await c.Inscribirse(new InscribirseRequest("plan", Ancla.AlDespertar, new TimeOnly(6, 0)), default));
        Assert.Equal($"/api/my-plans/{PlanId}", creado.Location);
        Assert.Equal(new InscribirseCommand("plan", Ancla.AlDespertar, new TimeOnly(6, 0)), servicio.Inscripcion);

        foreach (var accion in new Func<Task<IActionResult>>[]
        {
            () => c.Listar(default),
            () => c.Obtener(PlanId, default),
            () => c.CambiarEstado(PlanId, new CambiarEstadoPlanRequest(EstadoPlanUsuario.Pausado, MotivoPausa.Viaje, true), default),
            () => c.Ajustar(PlanId, new AjustarPlanRequest(TipoAjuste.Extender), default),
            () => c.Recordatorio(PlanId, new RecordatorioPlanRequest(new TimeOnly(7, 30), true), default),
        })
        {
            await accion();
            Assert.Equal(UsuarioId, servicio.UltimoUsuario);
        }

        Assert.Equal(new CambiarEstadoPlanCommand(EstadoPlanUsuario.Pausado, MotivoPausa.Viaje, true), servicio.Transicion);
        Assert.Equal(new AjustarPlanCommand(TipoAjuste.Extender), servicio.Ajuste);
        Assert.Equal(new ConfigurarRecordatorioCommand(new TimeOnly(7, 30), true), servicio.Recordatorio);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    public async Task RF_NEG_06_TokenSinIdentidadValida_SeRechaza()
    {
        var c = Controlador(new PlanesFalsos(), sub: "no-es-un-guid");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.Listar(default));
    }

    // ---------- Validación de la entrada ----------

    // En un record posicional los atributos de validación van en los parámetros del constructor, que es de donde los lee MVC.
    private static bool Valida<TRequest>(string parametro, object? valor)
    {
        var p = typeof(TRequest).GetConstructors()[0].GetParameters().Single(x => string.Equals(x.Name, parametro, StringComparison.OrdinalIgnoreCase));
        return Validator.TryValidateValue(valor!, new ValidationContext(new object()) { MemberName = p.Name }, [], p.GetCustomAttributes<ValidationAttribute>());
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Peticiones_ExigenLosCamposObligatoriosYRespetanLasLongitudes()
    {
        Assert.True(Valida<InscribirseRequest>("slug", "mi-dia-oficina"));
        Assert.False(Valida<InscribirseRequest>("slug", ""));
        Assert.False(Valida<InscribirseRequest>("slug", null));
        Assert.False(Valida<InscribirseRequest>("slug", new string('a', PlanUsuario.LongitudSlug + 1)));
        Assert.False(Valida<CambiarEstadoPlanRequest>("nuevoEstado", null));
        Assert.True(Valida<CambiarEstadoPlanRequest>("nuevoEstado", EstadoPlanUsuario.Cancelado));
        Assert.False(Valida<AjustarPlanRequest>("tipo", null));
        Assert.True(Valida<AjustarPlanRequest>("tipo", TipoAjuste.ModoSuave));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_LasHorasLlegaComoHHmmYLosEnumsComoTexto()
    {
        var inscripcion = JsonSerializer.Deserialize<InscribirseRequest>("""{"slug":"x","ancla":"AMediaTarde","hora":"07:30"}""", Web)!;
        var transicion = JsonSerializer.Deserialize<CambiarEstadoPlanRequest>("""{"nuevoEstado":"Pausado","motivoPausa":"Enfermedad"}""", Web)!;

        Assert.Equal(new TimeOnly(7, 30), inscripcion.Hora);
        Assert.Equal(Ancla.AMediaTarde, inscripcion.Ancla);
        Assert.Equal(EstadoPlanUsuario.Pausado, transicion.NuevoEstado);
        Assert.Equal(MotivoPausa.Enfermedad, transicion.MotivoPausa);
        Assert.False(transicion.ConservarHabitos);
    }

    // ---------- Errores: códigos estables, mensajes genéricos ----------

    private static async Task<(int Status, JsonElement Cuerpo)> EjecutarAsync(Exception excepcion)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        var middleware = new GlobalExceptionMiddleware(_ => throw excepcion, NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        contexto.Response.Body.Position = 0;
        return (contexto.Response.StatusCode, (await JsonDocument.ParseAsync(contexto.Response.Body)).RootElement.Clone());
    }

    [Theory]
    [InlineData(ReglaNegocioException.PlanNoApto)]
    [InlineData(ReglaNegocioException.CuestionarioIncompleto)]
    [InlineData(ReglaNegocioException.LimitePlanes)]
    [InlineData(ReglaNegocioException.PlanYaActivo)]
    [InlineData(ReglaNegocioException.LimiteExtensiones)]
    [InlineData(ReglaNegocioException.TransicionPlanInvalida)]
    [InlineData(ReglaNegocioException.PlanAunNoTermina)]
    [InlineData(ReglaNegocioException.AjusteNoDisponible)]
    [InlineData(ReglaNegocioException.VersionMinimaNoDisponible)]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_LasReglasDeMisPlanes_Responden400ConSuCodigoYSinDetallesInternos(string codigo)
    {
        var (status, cuerpo) = await EjecutarAsync(new ReglaNegocioException(codigo, "Mensaje para la persona."));

        Assert.Equal(400, status);
        Assert.Equal(codigo, cuerpo.GetProperty("codigo").GetString());
        Assert.DoesNotContain("Exception", cuerpo.GetRawText());
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_UnPlanQueNoExisteOEsAjeno_Responde404ConProblemDetails()
    {
        var (status, cuerpo) = await EjecutarAsync(
            new RecursoNoEncontradoException(RecursoNoEncontradoException.PlanUsuarioNoEncontrado, "No encontramos ese plan."));

        Assert.Equal(404, status);
        Assert.Equal("plan_usuario_no_encontrado", cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_LasConsultasDeHoyYResumen_NoSeGuardanEnCacheAunqueFallen()
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        contexto.Response.Headers.CacheControl = "public,max-age=300";
        var middleware = new GlobalExceptionMiddleware(_ => throw new TimeoutException(), NullLogger<GlobalExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(contexto);

        Assert.Equal("no-store", contexto.Response.Headers.CacheControl.ToString());
    }

    // ---------- Hoy y resumen ----------

    private sealed class HoyFalso : IHoyService
    {
        public Guid UltimoUsuario { get; private set; }

        public Task<HoyDto> ObtenerHoyAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            return Task.FromResult(new HoyDto(new DateOnly(2026, 11, 9), [], [], null, null, null, []));
        }
    }

    private sealed class ResumenFalso : IResumenProgresoService
    {
        public Guid UltimoUsuario { get; private set; }
        public int Vistos { get; private set; }

        public Task<ResumenProgresoDto> ObtenerResumenAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            return Task.FromResult(new ResumenProgresoDto(0, 0, [], new MesEnNumerosDto(0, 0, 0), null, 0));
        }

        public Task MarcarLogrosVistosAsync(Guid usuarioId, CancellationToken cancellationToken = default)
        {
            UltimoUsuario = usuarioId;
            Vistos++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-06")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_06_HoyResumenYLogrosVistos_UsanLaIdentidadDelToken()
    {
        var hoy = new HoyFalso();
        var resumen = new ResumenFalso();
        var c = new TodayController(hoy, resumen)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, UsuarioId.ToString())], "Bearer")),
                },
            },
        };

        Assert.IsType<OkObjectResult>(await c.Hoy(default));
        Assert.Equal(UsuarioId, hoy.UltimoUsuario);
        Assert.IsType<OkObjectResult>(await c.Resumen(default));
        Assert.IsType<NoContentResult>(await c.LogrosVistos(default));
        Assert.Equal(UsuarioId, resumen.UltimoUsuario);
        Assert.Equal(1, resumen.Vistos);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_RegistrarAvance_AceptaEsVersionMinimaYPorDefectoEsFalso()
    {
        var sin = JsonSerializer.Deserialize<RegistrarAvanceRequest>("""{"cantidad":1,"fechaUtc":"2026-11-09T14:00:00Z"}""", Web)!;
        var con = JsonSerializer.Deserialize<RegistrarAvanceRequest>("""{"cantidad":1,"fechaUtc":"2026-11-09T14:00:00Z","esVersionMinima":true}""", Web)!;

        Assert.False(sin.EsVersionMinima);
        Assert.True(con.EsVersionMinima);
    }
}
