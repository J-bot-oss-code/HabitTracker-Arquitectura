using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Auditing.Application.Interfaces;
using Moq;
using WebApi.Adapters;

namespace Auditing.Api.Tests;

/// <summary>El adaptador que lleva la resolución de una solicitud de permiso al historial (RF-AUD-02), con dobles y sin base de datos (RD-12).</summary>
public class AuditorPermisosAdapterTests
{
    [Fact]
    [Trait("Requirement", "RF-AUD-02")]
    public async Task RF_AUD_02_ElAdaptadorBuscaAlAdministradorPorSuCorreoYGuardaLaResolucionSobreLaSolicitud()
    {
        // Arrange
        var admin = new Usuario("Admin", new Email("admin@ejemplo.com"), "hash", Rol.Administrador);
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.GetByCorreoAsync(It.Is<Email>(e => e.Valor == "admin@ejemplo.com"))).ReturnsAsync(admin);
        var historial = new Mock<IRegistroAuditoriaService>();
        var solicitudId = Guid.NewGuid();
        var adaptador = new AuditorPermisosAdapter(historial.Object, usuarios.Object);

        // Act
        await adaptador.ResolucionRegistradaAsync(
            "admin@ejemplo.com", solicitudId, "Aprobación de solicitud de permiso", "Pendiente", "Aplicada (permiso «X» de ana@ejemplo.com)");

        // Assert
        historial.Verify(h => h.RegistrarAsync(
            admin.Id, "admin@ejemplo.com", "Aprobación de solicitud de permiso", "SolicitudPermiso", solicitudId.ToString(),
            "Pendiente", "Aplicada (permiso «X» de ana@ejemplo.com)", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-02")]
    public async Task RF_AUD_02_SiNoSePuedeIdentificarAlAdministrador_LaResolucionFallaYNoSeRegistraNada()
    {
        // Arrange
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.GetByCorreoAsync(It.IsAny<Email>())).ReturnsAsync((Usuario?)null);
        var historial = new Mock<IRegistroAuditoriaService>();
        var adaptador = new AuditorPermisosAdapter(historial.Object, usuarios.Object);

        // Act y Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adaptador.ResolucionRegistradaAsync("nadie@ejemplo.com", Guid.NewGuid(), "Rechazo de solicitud de permiso", "Pendiente", "Rechazada"));
        historial.VerifyNoOtherCalls();
    }
}
