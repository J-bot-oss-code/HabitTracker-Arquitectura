using AccessControl.Domain.Interfaces;
using Moq;

namespace AccessControl.Application.Tests;

/// <summary>En los dobles del repositorio, la reserva atómica del intento de recuperación siempre concede (la prueba real es de SQLite).</summary>
internal static class ReservaDeRecuperacionDePrueba
{
    public static Mock<IUsuarioRepository> ConReservaDeRecuperacion(this Mock<IUsuarioRepository> repo)
    {
        repo.Setup(r => r.ReservarIntentoRecuperacionAsync(It.IsAny<Guid>())).ReturnsAsync(1);
        return repo;
    }
}
