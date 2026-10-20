using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace AccessControl.Application.Tests;

public class UsuarioServiceTests
{
    [Fact]
    public async Task AutenticarUsuarioAsync_PasswordIncorrecta_RechazaYRegistraFallo()
    {
        // Arrange
        var usuario = new Usuario(
            "Persona de prueba",
            new Email("persona@ejemplo.com"),
            "hash-guardado");

        var repositorio = new Mock<IUsuarioRepository>();
        repositorio
            .Setup(r => r.GetByCorreoAsync(It.IsAny<Email>()))
            .ReturnsAsync(usuario);
        repositorio
            .Setup(r => r.UpdateAsync(usuario))
            .Returns(Task.CompletedTask);

        var verificadorPassword = new Mock<IPasswordHasher>();
        verificadorPassword
            .Setup(v => v.verificar("hash-guardado", "clave-incorrecta"))
            .Returns(false);

        var servicio = new UsuarioService(
            repositorio.Object,
            verificadorPassword.Object,
            new Mock<IEmailQueue>().Object,
            new Mock<IAuditoriaService>().Object,
            new Mock<IJwtProvider>().Object,
            new Mock<ITokenBlacklist>().Object,
            new Mock<ILogger<UsuarioService>>().Object);

        var solicitud = new LoginRequestDto(
            "persona@ejemplo.com",
            "clave-incorrecta");

        // Act
        var error = await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => servicio.AutenticarUsuarioAsync(solicitud));

        // Assert
        Assert.Equal("Credenciales incorrectas", error.Message);
        Assert.Equal(1, usuario.Bloqueo.IntentosFallidos);
        repositorio.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }
}