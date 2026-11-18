using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using WebApi.Controllers;

namespace AccessControl.Application.Tests;

/// <summary>La cola de correos solo la consulta y la procesa un Administrador (RF-NOT-11, RD-06); la lógica no vive en el controller (RD-02).</summary>
public class AdminColaCorreosControllerTests
{
    [Fact]
    [Trait("Requirement", "RD-06")]
    public void RD_06_ElControllerEsSoloParaAdministradores_UnEstandarRecibe403()
    {
        var autorizacion = Assert.Single(typeof(AdminColaCorreosController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: false)
            .Cast<AuthorizeAttribute>());

        Assert.Equal("Administrador", autorizacion.Roles);
        // Ninguna acción lo relaja con [AllowAnonymous].
        Assert.DoesNotContain(typeof(AdminColaCorreosController).GetMethods(), m => m.GetCustomAttributes(typeof(AllowAnonymousAttribute), false).Length > 0);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-11")]
    public async Task RF_NOT_11_ListarDelegaElFiltroEnElServicio()
    {
        var cola = new Mock<IColaCorreosAdmin>();
        CorreoColaDto[] correos = [new(Guid.NewGuid(), "ana@ejemplo.com", "Asunto", "Fallido", 3, DateTime.UtcNow, DateTime.UtcNow, null, "Servidor apagado")];
        cola.Setup(c => c.ListarAsync("Fallido", It.IsAny<CancellationToken>())).ReturnsAsync(correos);

        var resultado = await new AdminColaCorreosController(cola.Object).Listar("Fallido", CancellationToken.None);

        Assert.Same(correos, Assert.IsType<OkObjectResult>(resultado).Value);
    }

    [Fact]
    [Trait("Requirement", "RF-NOT-09")]
    public async Task RF_NOT_09_ProcesarDelegaEnElServicioYDevuelveLosConteos()
    {
        var cola = new Mock<IColaCorreosAdmin>();
        cola.Setup(c => c.ProcesarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new ResultadoColaDto(2, 1, 0));

        var resultado = await new AdminColaCorreosController(cola.Object).Procesar(CancellationToken.None);

        Assert.Equal(new ResultadoColaDto(2, 1, 0), Assert.IsType<OkObjectResult>(resultado).Value);
    }
}
