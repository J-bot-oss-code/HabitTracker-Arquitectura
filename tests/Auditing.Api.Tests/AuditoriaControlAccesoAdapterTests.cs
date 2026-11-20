using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Auditing.Application.Interfaces;
using Moq;
using WebApi.Adapters;

namespace Auditing.Api.Tests;

/// <summary>El adaptador que lleva los eventos de Control de Acceso al historial (RF-AUD-01 y 04), con dobles y sin base de datos (RD-12).</summary>
public class AuditoriaControlAccesoAdapterTests
{
    [Fact]
    [Trait("Requirement", "RF-AUD-01")]
    public async Task RF_AUD_01_ElAdaptadorGuardaElCorreoDeQuienActuo_BuscadoPorSuIdentificador_ConTodosLosDemasDatos()
    {
        // Arrange
        var admin = new Usuario("Admin", new Email("admin@ejemplo.com"), "hash", Rol.Administrador);
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.GetByIdAsync(admin.Id)).ReturnsAsync(admin);
        var historial = new Mock<IRegistroAuditoriaService>();
        var adaptador = new AuditoriaControlAccesoAdapter(historial.Object, usuarios.Object);

        // Act: el segundo parámetro llega con el rol, no con el nombre
        await adaptador.RegistroAuditoriaAsync(admin.Id, "Administrador", "Cambio de rol", "Usuario", "u-1", "Estandar", "Administrador");

        // Assert
        historial.Verify(h => h.RegistrarAsync(
            admin.Id, "admin@ejemplo.com", "Cambio de rol", "Usuario", "u-1", "Estandar", "Administrador", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-01")]
    public async Task RF_AUD_01_SiLaCuentaDeQuienActuoYaNoExiste_ElRegistroSeGuardaConSuIdentificador()
    {
        // Arrange
        var id = Guid.NewGuid();
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.GetByIdAsync(id)).ReturnsAsync((Usuario?)null);
        var historial = new Mock<IRegistroAuditoriaService>();
        var adaptador = new AuditoriaControlAccesoAdapter(historial.Object, usuarios.Object);

        // Act
        await adaptador.RegistroAuditoriaAsync(id, "Estandar", "Reactivación de cuenta", "Usuario", id.ToString(), "Inactivo", "Activo");

        // Assert
        historial.Verify(h => h.RegistrarAsync(
            id, id.ToString(), "Reactivación de cuenta", "Usuario", id.ToString(), "Inactivo", "Activo", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-01")]
    public async Task RF_AUD_01_UnFalloAlGuardarElRegistroNoSeTraga_LaOperacionQueLoOrigino_Falla()
    {
        // Arrange
        var admin = new Usuario("Admin", new Email("admin@ejemplo.com"), "hash", Rol.Administrador);
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.GetByIdAsync(admin.Id)).ReturnsAsync(admin);
        var historial = new Mock<IRegistroAuditoriaService>();
        historial.Setup(h => h.RegistrarAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("la base de datos no responde"));
        var adaptador = new AuditoriaControlAccesoAdapter(historial.Object, usuarios.Object);

        // Act y Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adaptador.RegistroAuditoriaAsync(admin.Id, "Administrador", "Cambio de rol", "Usuario", "u-1", "Estandar", "Administrador"));
    }
}
