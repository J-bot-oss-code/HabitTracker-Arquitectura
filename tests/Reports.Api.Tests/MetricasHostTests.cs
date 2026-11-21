using System.Reflection;
using AccessControl.Domain.Interfaces;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Sugerencias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Moq;
using Reports.Application.DTOs;
using Reports.Application.Interfaces;
using WebApi.Adapters;
using WebApi.Controllers;
using WebApi.Extensions;

namespace Reports.Api.Tests;

/// <summary>Las métricas del Administrador en el Host, con dobles (RD-12): el rol, el adaptador que junta los módulos y su privacidad.</summary>
public class MetricasHostTests
{
    private static readonly DateTime Ahora = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    // ---------- Controller ----------

    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_ElEndpointExigeAdministradorEnElServidor_ConLimitePorPersona()
    {
        var tipo = typeof(MetricasAdminController);

        Assert.Equal("Administrador", Assert.Single(tipo.GetCustomAttributes<AuthorizeAttribute>()).Roles);
        Assert.Equal(LimitacionDePeticionesExtensions.Usuario, tipo.GetCustomAttribute<EnableRateLimitingAttribute>()!.PolicyName);
        // Sin parámetros de consulta: no hay forma de pedir datos de una persona concreta.
        Assert.Equal(["cancellationToken"], tipo.GetMethod(nameof(MetricasAdminController.Obtener))!.GetParameters().Select(p => p.Name));
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_ElEndpointResponde200ConLasMetricasDelServicio()
    {
        var esperado = new MetricasProductoDto(Ahora, 5, 3, 1, 2, 4, 20, 70, [new PlanIniciadoDto("caminar", 9)]);
        var servicio = new Mock<IMetricasProductoService>();
        servicio.Setup(s => s.ObtenerAsync(It.IsAny<CancellationToken>())).ReturnsAsync(esperado);

        var resultado = await new MetricasAdminController(servicio.Object).Obtener(CancellationToken.None);

        Assert.Same(esperado, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    // ---------- Adaptador ----------

    private static (FuenteMetricasAdapter Adaptador, Mock<IUsuarioRepository> Usuarios, Mock<IMetricasHabitosRepository> Habitos) Crear()
    {
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.ContarCreadasDesdeAsync(It.IsAny<DateTime>())).ReturnsAsync(3);
        usuarios.Setup(u => u.ContarConBajaProgramadaAsync()).ReturnsAsync(1);
        var habitos = new Mock<IMetricasHabitosRepository>();
        habitos.Setup(h => h.ContarPersonasConRegistroAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(12);
        habitos.Setup(h => h.ContarRetencionAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((8, 6));
        habitos.Setup(h => h.ContarRegistrosPorCorreoDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(4);
        habitos.Setup(h => h.PlanesMasIniciadosAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PlanIniciadoConteo("caminar", 9), new PlanIniciadoConteo("agua", 6)]);
        var sugerencias = new Mock<ISugerenciaRepository>();
        sugerencias.Setup(s => s.ContarNuevasAsync(It.IsAny<CancellationToken>())).ReturnsAsync(2);
        return (new FuenteMetricasAdapter(usuarios.Object, habitos.Object, sugerencias.Object), usuarios, habitos);
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_ElAdaptadorJuntaLosConteosDeLosTresModulos()
    {
        var (adaptador, _, _) = Crear();

        var c = await adaptador.ObtenerAsync(Ahora);

        Assert.Equal((3, 1, 2, 12, 8, 6, 4), (c.CuentasNuevas7d, c.EliminacionesEnCurso, c.SugerenciasNuevas, c.PersonasActivas30d, c.CohorteRetencion, c.PersonasRetenidas, c.RegistrosPorCorreo7d));
        Assert.Equal([("caminar", 9), ("agua", 6)], c.PlanesIniciados.Select(p => (p.Slug, p.Cantidad)));
    }

    [Fact]
    [Trait("Requirement", "RF-REP-04")]
    public async Task RF_REP_04_LasVentanasSonLasDeLaDefinicion_7_30_YCohorteDe30A60Dias()
    {
        var (adaptador, usuarios, habitos) = Crear();

        await adaptador.ObtenerAsync(Ahora);

        usuarios.Verify(u => u.ContarCreadasDesdeAsync(Ahora.AddDays(-7)), Times.Once);
        habitos.Verify(h => h.ContarPersonasConRegistroAsync(Ahora.AddDays(-30), It.Is<DateTime>(d => d > Ahora), It.IsAny<CancellationToken>()), Times.Once);
        habitos.Verify(h => h.ContarRetencionAsync(
            Ahora.AddDays(-60), Ahora.AddDays(-30), Ahora.AddDays(-30), It.Is<DateTime>(d => d > Ahora), It.IsAny<CancellationToken>()), Times.Once);
        habitos.Verify(h => h.ContarRegistrosPorCorreoDesdeAsync(Ahora.AddDays(-7), It.IsAny<CancellationToken>()), Times.Once);
        habitos.Verify(h => h.PlanesMasIniciadosAsync(FuenteMetricasAdapter.MaximoPlanes, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RD-06")]
    public async Task RD_06_ElAdaptadorSoloPideConteos_NuncaListasDeUsuariosNiDeHabitos()
    {
        var (adaptador, usuarios, habitos) = Crear();

        await adaptador.ObtenerAsync(Ahora);

        // Solo las consultas de conteo que declara el puerto: nada más se llamó sobre los repositorios.
        usuarios.VerifyAll();
        habitos.VerifyAll();
        usuarios.VerifyNoOtherCalls();
        habitos.VerifyNoOtherCalls();
    }
}
