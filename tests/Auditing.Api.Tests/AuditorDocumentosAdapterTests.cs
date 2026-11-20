using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Auditing.Application.Interfaces;
using Moq;
using WebApi.Adapters;

namespace Auditing.Api.Tests;

/// <summary>El adaptador que lleva las subidas y eliminaciones de documentos al historial (RF-AUD-03), con dobles y sin base de datos (RD-12).</summary>
public class AuditorDocumentosAdapterTests
{
    [Fact]
    [Trait("Requirement", "RF-AUD-03")]
    public async Task RF_AUD_03_ElAdaptadorGuardaElCorreoDeQuienActuoYElDocumentoComoEntidad()
    {
        // Arrange
        var ana = new Usuario("Ana", new Email("ana@ejemplo.com"), "hash");
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.GetByIdAsync(ana.Id)).ReturnsAsync(ana);
        var historial = new Mock<IRegistroAuditoriaService>();
        var documentoId = Guid.NewGuid();
        var adaptador = new AuditorDocumentosAdapter(historial.Object, usuarios.Object);

        // Act
        await adaptador.RegistrarAsync(ana.Id, "Subida de documento", documentoId, "No existía", "informe.pdf (4 bytes)");

        // Assert
        historial.Verify(h => h.RegistrarAsync(
            ana.Id, "ana@ejemplo.com", "Subida de documento", "Documento", documentoId.ToString(),
            "No existía", "informe.pdf (4 bytes)", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Requirement", "RF-AUD-03")]
    public async Task RF_AUD_03_UnFalloAlGuardarElRegistroNoSeTraga()
    {
        // Arrange
        var usuarios = new Mock<IUsuarioRepository>();
        usuarios.Setup(u => u.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);
        var historial = new Mock<IRegistroAuditoriaService>();
        historial.Setup(h => h.RegistrarAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("la base de datos no responde"));
        var adaptador = new AuditorDocumentosAdapter(historial.Object, usuarios.Object);

        // Act y Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => adaptador.RegistrarAsync(Guid.NewGuid(), "Eliminación de documento", Guid.NewGuid(), "a (visible)", "a (eliminado)"));
    }
}
