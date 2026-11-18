using System.Security.Claims;
using HabitTracker.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moq;
using Permissions.Application.Interfaces;
using Reports.Application.DTOs;
using Reports.Application.Interfaces;
using WebApi.Adapters;
using WebApi.Controllers;
using WebApi.Extensions;

namespace Reports.Api.Tests;

/// <summary>Controller y adaptadores de Reportes en el Host, con dobles (RD-12): sin API ni base de datos.</summary>
public class ReportesHostTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly DateOnly Desde = new(2026, 11, 2);
    private static readonly DateOnly Hasta = new(2026, 11, 8);

    private static (ReportsController Controlador, Mock<IReportesService> Servicio) Crear(string rol, string? sub = null)
    {
        var servicio = new Mock<IReportesService>();
        var identidad = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, sub ?? Ana.ToString()),
            new Claim(ClaimTypes.Email, "ana@ejemplo.com"),
            new Claim(ClaimTypes.Role, rol),
        ], "Bearer");
        var controlador = new ReportsController(servicio.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identidad) } }
        };
        return (controlador, servicio);
    }

    // ---------- Controller ----------

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_ElControllerExigeSesion_TieneLimiteDePeticiones_YNingunaAccionLaRelaja()
    {
        Assert.Single(typeof(ReportsController).GetCustomAttributes(typeof(AuthorizeAttribute), false));
        var limite = Assert.Single(typeof(ReportsController).GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>());
        Assert.Equal(LimitacionDePeticionesExtensions.Usuario, limite.PolicyName);
        Assert.DoesNotContain(typeof(ReportsController).GetMethods(), m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), false).Length > 0);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_UnEstandarConsultaConSuIdentidadYSuCorreoDelToken_SinPoderElegirOtro()
    {
        var (c, servicio) = Crear("Estandar");
        ConsultaReporte? recibida = null;
        servicio.Setup(s => s.PermisosPorEstadoAsync(It.IsAny<ConsultaReporte>(), It.IsAny<CancellationToken>()))
            .Callback<ConsultaReporte, CancellationToken>((q, _) => recibida = q)
            .ReturnsAsync(new ReportePermisosDto(Desde, Hasta, "propio", [], 0));

        var resultado = await c.Permisos(Desde, Hasta, CancellationToken.None);

        Assert.IsType<OkObjectResult>(resultado);
        Assert.Equal(new ConsultaReporte(Ana, "ana@ejemplo.com", false, Desde, Hasta), recibida);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_ElRolAdministradorSaleDelToken()
    {
        var (c, servicio) = Crear("Administrador");
        ConsultaReporte? recibida = null;
        servicio.Setup(s => s.ConstanciaPorPlanAsync(It.IsAny<ConsultaReporte>(), It.IsAny<CancellationToken>()))
            .Callback<ConsultaReporte, CancellationToken>((q, _) => recibida = q)
            .ReturnsAsync(new ReporteConstanciaDto(Desde, Hasta, "todos", "pregunta", []));

        await c.Constancia(Desde, Hasta, CancellationToken.None);

        Assert.True(recibida!.EsAdministrador);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_UnTokenSinIdentidadValida_NoConsultaNada()
    {
        var (c, servicio) = Crear("Estandar", sub: "no-es-un-guid");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.Permisos(Desde, Hasta, CancellationToken.None));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => c.Constancia(Desde, Hasta, CancellationToken.None));

        servicio.VerifyNoOtherCalls();
    }

    // ---------- Adaptadores ----------

    [Fact]
    [Trait("Requirement", "RF-REP-01")]
    public async Task RF_REP_01_ElAdaptadorDelCoreTraeLasFilasYaAgrupadasDeLaBaseDeDatos()
    {
        var permisos = new Mock<ISolicitudPermisoReportes>();
        var desde = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
        var hasta = desde.AddDays(7);
        permisos.Setup(p => p.ContarPorEstadoAsync(desde, hasta, "ana@ejemplo.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ConteoPorEstado("Pendiente", 2), new ConteoPorEstado("Aplicada", 1)]);

        var filas = await new FuenteSolicitudesAdapter(permisos.Object).ContarPorEstadoAsync(desde, hasta, "ana@ejemplo.com");

        Assert.Equal([new FilaPermisosDto("Pendiente", 2), new FilaPermisosDto("Aplicada", 1)], filas);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-02")]
    public async Task RF_REP_02_ElAdaptadorDelNegocioTraeLaConstanciaPersonalYLaGlobalDeHabitTracker()
    {
        var constancia = new Mock<IReporteConstanciaService>();
        constancia.Setup(c => c.PersonalAsync(Ana, Desde, Hasta, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new HabitTracker.Application.Services.FilaConstanciaDto("Calma", 1, 3, 1, 5, 60m)]);
        constancia.Setup(c => c.GlobalAsync(Desde, Hasta, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new HabitTracker.Application.Services.FilaConstanciaDto("Calma", 4, 3.5m, 1m, 5m, 70m)]);
        var adaptador = new FuenteConstanciaAdapter(constancia.Object);

        var personal = await adaptador.PersonalAsync(Ana, Desde, Hasta);
        var global = await adaptador.GlobalAsync(Desde, Hasta);

        Assert.Equal(new Reports.Application.DTOs.FilaConstanciaDto("Calma", 1, 3, 1, 5, 60m), Assert.Single(personal));
        Assert.Equal(new Reports.Application.DTOs.FilaConstanciaDto("Calma", 4, 3.5m, 1m, 5m, 70m), Assert.Single(global));
    }
}
