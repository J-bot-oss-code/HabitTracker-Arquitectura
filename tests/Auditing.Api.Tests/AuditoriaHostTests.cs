using System.Reflection;
using System.Security.Claims;
using Auditing.Application.DTOs;
using Auditing.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using WebApi.Controllers;
using WebApi.Extensions;

namespace Auditing.Api.Tests;

/// <summary>Controller de la consulta del historial (RF-AUD-06 y 07) con dobles: sin levantar la API ni la base de datos (RD-12).</summary>
public class AuditoriaHostTests
{
    private static ClaimsPrincipal Principal(string rol) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, rol), new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "Bearer"));

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_UnEstandarNoConsultaElHistorial_ElServidorLoRechazaConUn403_YElAdministradorSiPasa()
    {
        // Arrange: la política que el servidor aplica a la ruta sale del atributo del propio controller
        var authorize = Assert.Single(typeof(AuditController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>());
        Assert.Equal("Administrador", authorize.Roles);
        var policy = new AuthorizationPolicyBuilder().RequireRole(authorize.Roles!.Split(',')).Build();
        using var provider = new ServiceCollection().AddLogging().AddAuthorization().BuildServiceProvider();
        var autorizacion = provider.GetRequiredService<IAuthorizationService>();
        var estandar = Principal("Estandar");

        // Act
        var resultadoEstandar = await autorizacion.AuthorizeAsync(estandar, resource: null, policy);
        var resultadoAdministrador = await autorizacion.AuthorizeAsync(Principal("Administrador"), resource: null, policy);
        var autenticacion = new Mock<IAuthenticationService>();
        autenticacion
            .Setup(s => s.ForbidAsync(It.IsAny<HttpContext>(), It.IsAny<string?>(), It.IsAny<AuthenticationProperties?>()))
            .Callback<HttpContext, string?, AuthenticationProperties?>((contexto, _, _) => contexto.Response.StatusCode = StatusCodes.Status403Forbidden)
            .Returns(Task.CompletedTask);
        using var httpProvider = new ServiceCollection().AddLogging().AddSingleton(autenticacion.Object).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = httpProvider, User = estandar };
        await new AuthorizationMiddlewareResultHandler().HandleAsync(
            _ => Task.CompletedTask, http, policy, PolicyAuthorizationResult.Forbid(resultadoEstandar.Failure));

        // Assert
        Assert.False(resultadoEstandar.Succeeded);
        Assert.Contains(resultadoEstandar.Failure!.FailedRequirements, r => r is RolesAuthorizationRequirement);
        Assert.True(resultadoAdministrador.Succeeded);
        Assert.Equal(StatusCodes.Status403Forbidden, http.Response.StatusCode);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_LaConsultaExigeElRolEnElServidor_TieneLimiteDePeticiones_YNingunaAccionLoRelaja()
    {
        var limite = Assert.Single(typeof(AuditController).GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>());
        Assert.Equal(LimitacionDePeticionesExtensions.Usuario, limite.PolicyName);
        Assert.DoesNotContain(typeof(AuditController).GetMethods(), m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), false).Length > 0);
        Assert.DoesNotContain(typeof(AuditController).GetMethods(), m => m.GetCustomAttributes(typeof(AuthorizeAttribute), false).Length > 0);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-06")]
    public void RF_AUD_06_ElControllerSoloTieneUnaOperacionYEsUnGet_NoExisteEdicionNiBorrado()
    {
        // Arrange
        var acciones = typeof(AuditController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToList();

        // Act
        var verbos = acciones.SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>()).SelectMany(a => a.HttpMethods).ToList();

        // Assert
        Assert.Single(acciones);
        Assert.Equal(["GET"], verbos);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-07")]
    public async Task RF_AUD_07_ElControllerPasaCadaFiltroAlServicio_YDevuelveLaPaginaTalCual()
    {
        // Arrange
        var servicio = new Mock<IRegistroAuditoriaService>();
        ConsultaAuditoria? recibida = null;
        var pagina = new PaginaAuditoriaDto(1, 2, 25, [new RegistroAuditoriaDto(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), "a@ejemplo.com", "Cambio de rol", "Usuario", "u1", "Estandar", "Administrador")]);
        servicio.Setup(s => s.ConsultarAsync(It.IsAny<ConsultaAuditoria>(), It.IsAny<CancellationToken>()))
            .Callback<ConsultaAuditoria, CancellationToken>((c, _) => recibida = c)
            .ReturnsAsync(pagina);
        var controlador = new AuditController(servicio.Object);

        // Act
        var resultado = await controlador.Consultar(
            "ana@", "Documento", "d1", "Subida de documento", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30),
            CancellationToken.None, pagina: 2, tamano: 25);

        // Assert
        Assert.Same(pagina, Assert.IsType<OkObjectResult>(resultado).Value);
        Assert.Equal(new ConsultaAuditoria("ana@", "Documento", "d1", "Subida de documento", new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), 2, 25), recibida);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_MiActividad_ExigeSesionYNoRecibeAQuienConsultar()
    {
        var tipo = typeof(MiActividadController);
        var authorize = Assert.Single(tipo.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true).Cast<AuthorizeAttribute>());
        Assert.Null(authorize.Roles); // ambos roles, cada uno solo lo suyo

        // La persona sale del token: ningún parámetro de la acción puede pedir la actividad de otra.
        var parametros = tipo.GetMethod(nameof(MiActividadController.Consultar))!.GetParameters().Select(p => p.Name);
        Assert.Equal(["cancellationToken", "dias", "pagina", "tamano"], parametros);
        Assert.Equal(["GET"], tipo.GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), true))
            .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>().SelectMany(a => a.HttpMethods));
    }
}
