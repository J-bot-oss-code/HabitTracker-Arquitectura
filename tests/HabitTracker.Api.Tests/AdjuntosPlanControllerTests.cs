using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using DocumentManager.Application.Exceptions;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Excepciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using WebApi.Controllers;
using WebApi.Middlewares;

namespace HabitTracker.Api.Tests;

/// <summary>Adjuntos de un plan y el 409 «documento_requiere_permiso», sin levantar la API (RD-12).</summary>
public class AdjuntosPlanControllerTests
{
    private static readonly Guid Usuario = Guid.NewGuid();
    private const string Correo = "ana@ejemplo.com";

    private sealed class ServicioFalso : IAdjuntosPlanService
    {
        public (Guid Usuario, string Correo, Guid Plan, Guid Documento)? UltimoQuitar { get; private set; }
        public (Guid Usuario, Guid Plan, Guid Documento)? UltimoAdjuntar { get; private set; }

        public Task<IReadOnlyList<AdjuntoPlanDto>> ListarDePlanAsync(Guid usuarioId, Guid planId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AdjuntoPlanDto>>([]);
        public Task<IReadOnlyList<VinculoDocumentoDto>> ListarVinculosAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<VinculoDocumentoDto>>([]);
        public Task AdjuntarAsync(Guid usuarioId, Guid planId, Guid documentoId, CancellationToken cancellationToken = default)
        {
            UltimoAdjuntar = (usuarioId, planId, documentoId);
            return Task.CompletedTask;
        }
        public Task QuitarAsync(Guid usuarioId, string correo, Guid planId, Guid documentoId, CancellationToken cancellationToken = default)
        {
            UltimoQuitar = (usuarioId, correo, planId, documentoId);
            return Task.CompletedTask;
        }
    }

    private static (AdjuntosPlanController Controlador, ServicioFalso Servicio) Crear()
    {
        var servicio = new ServicioFalso();
        var identidad = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Usuario.ToString()), new Claim(ClaimTypes.Email, Correo)], "prueba");
        var controlador = new AdjuntosPlanController(servicio)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) } },
        };
        return (controlador, servicio);
    }

    private static async Task<(int Status, JsonElement Cuerpo)> EjecutarAsync(Exception excepcion)
    {
        var contexto = new DefaultHttpContext();
        contexto.Response.Body = new MemoryStream();
        await new GlobalExceptionMiddleware(_ => throw excepcion, NullLogger<GlobalExceptionMiddleware>.Instance).InvokeAsync(contexto);
        contexto.Response.Body.Position = 0;
        var cuerpo = await JsonDocument.ParseAsync(contexto.Response.Body);
        return (contexto.Response.StatusCode, cuerpo.RootElement.Clone());
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_ElControlador_ExigeRolEstandar_ConRutaDeLosPlanes_YSinCache()
    {
        var tipo = typeof(AdjuntosPlanController);

        Assert.Equal("Estandar", tipo.GetCustomAttribute<AuthorizeAttribute>()!.Roles);
        Assert.Equal("api/my-plans", tipo.GetCustomAttribute<RouteAttribute>()!.Template);
        Assert.True(tipo.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-10")]
    [Trait("Requirement", "RD-06")]
    public async Task RF_NEG_10_Adjuntar_Y_Quitar_UsanLaIdentidadDelToken_NuncaLaDelCuerpo()
    {
        var (c, servicio) = Crear();
        var plan = Guid.NewGuid();
        var documento = Guid.NewGuid();

        Assert.IsType<NoContentResult>(await c.Adjuntar(plan, new AdjuntarArchivoRequest(documento), CancellationToken.None));
        Assert.IsType<NoContentResult>(await c.Quitar(plan, documento, CancellationToken.None));

        Assert.Equal((Usuario, plan, documento), servicio.UltimoAdjuntar);
        Assert.Equal((Usuario, Correo, plan, documento), servicio.UltimoQuitar);
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_PermisoRequerido_Es409ConCodigo()
    {
        var (status, cuerpo) = await EjecutarAsync(new PermisoRequeridoException());

        Assert.Equal(409, status);
        Assert.Equal("documento_requiere_permiso", cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_DocumentoProtegido_DelManejador_TambienEs409ConElMismoCodigo()
    {
        var (status, cuerpo) = await EjecutarAsync(new DocumentoProtegidoException());

        Assert.Equal(409, status);
        Assert.Equal("documento_requiere_permiso", cuerpo.GetProperty("codigo").GetString());
    }

    [Theory]
    [InlineData(ReglaNegocioException.PlanNoEnCurso)]
    [InlineData(ReglaNegocioException.AdjuntoDuplicado)]
    [InlineData(ReglaNegocioException.LimiteAdjuntos)]
    [Trait("Requirement", "RD-08")]
    public async Task RD_08_LasReglasDeAdjuntos_Son400ConSuCodigo(string codigo)
    {
        var (status, cuerpo) = await EjecutarAsync(new ReglaNegocioException(codigo, "Mensaje."));

        Assert.Equal(400, status);
        Assert.Equal(codigo, cuerpo.GetProperty("codigo").GetString());
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public void RD_07_Adjuntar_SinDocumentoId_EsUnaPeticionInvalida()
    {
        var p = typeof(AdjuntarArchivoRequest).GetConstructors().Single().GetParameters().Single();
        var atributos = p.GetCustomAttributes<System.ComponentModel.DataAnnotations.ValidationAttribute>();
        var contexto = new System.ComponentModel.DataAnnotations.ValidationContext(new object()) { MemberName = p.Name };

        Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateValue(null!, contexto, [], atributos));
        Assert.True(System.ComponentModel.DataAnnotations.Validator.TryValidateValue(Guid.NewGuid(), contexto, [], atributos));
    }
}
