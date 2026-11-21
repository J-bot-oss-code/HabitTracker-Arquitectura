using Microsoft.Extensions.Configuration;
using WebApi.Extensions;

namespace DocumentManager.Api.Tests;

/// <summary>Comprobaciones de arranque para producción (RD-09, RD-12): zona horaria disponible y semilla de demostración opcional.</summary>
public class VerificacionDelEntornoTests
{
    [Fact]
    public void ExigirZonasHorarias_ConLasZonasDelProducto_NoFalla()
    {
        // Act y Assert
        VerificacionDelEntorno.ExigirZonasHorarias();
    }

    [Fact]
    public void ExigirZonasHorarias_ConUnaZonaQueElSistemaNoConoce_DetieneElArranqueConUnMensajeClaro()
    {
        // Act
        var error = Assert.Throws<InvalidOperationException>(() => VerificacionDelEntorno.ExigirZonasHorarias(["Zona/Inexistente"]));

        // Assert
        Assert.Contains("Zona/Inexistente", error.Message);
        Assert.Contains("tzdata", error.Message);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void DebeSembrarDemostracion_PorDefectoSi_YSeDesactivaConLaConfiguracion(string? valor, bool esperado)
    {
        // Arrange
        var datos = valor is null ? [] : new Dictionary<string, string?> { ["BaseDeDatos:SembrarDemostracion"] = valor };
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(datos).Build();

        // Act y Assert
        Assert.Equal(esperado, VerificacionDelEntorno.DebeSembrarDemostracion(configuracion));
    }
}
