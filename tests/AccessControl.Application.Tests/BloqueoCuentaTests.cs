using AccessControl.Domain.Entities.ValueObjects;
namespace AccessControl.Application.Tests;

public class BloqueoCuentaTests
{
    [Fact]
    public void RegistraFallo_UnIntento_NoBloquea_Cuenta()
    {
        // Arrange
        var bloqueo = new BloqueoCuenta();

        // Act
        bloqueo.RegistrarFallo();

        // Assert
        Assert.Equal(1, bloqueo.IntentosFallidos);
        Assert.False(bloqueo.EstaBloqueado());
    }

    [Fact]
    public void RegistraFallo_QuintoIntento_BloqueaLaCuenta()
    {
        // Arrange
        var cuentaBloqueada = new BloqueoCuenta();

        // Act
        for(var intentos = 0; intentos <5; intentos++)
        {
            cuentaBloqueada.RegistrarFallo();
        }

        // Assert
        Assert.Equal(5, cuentaBloqueada.IntentosFallidos);
        Assert.True(cuentaBloqueada.EstaBloqueado());
    }
}
