using HabitTracker.Domain.Entities;

namespace HabitTracker.Domain.Tests;

public class RegistroDiarioTests
{
    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_02_RegistroDiario_CantidadPositivaFechaUtc_Valido()
    {
        // Arrange
        var fechaUtc = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

        // Act
        var registro = new RegistroDiario(5, fechaUtc, "paginas");

        // Assert
        Assert.NotEqual(Guid.Empty, registro.Id);
        Assert.Equal(5, registro.Cantidad);
        Assert.Equal(DateTimeKind.Utc, registro.Fecha.Kind);
        Assert.Equal("paginas", registro.Unidad);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_RegistroDiario_CantidadCero_Rechaza()
    {
        // Arrange & Act
        var excepcion = Record.Exception(() => new RegistroDiario(0, DateTime.UtcNow, "paginas"));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_RegistroDiario_CantidadNegativa_Rechaza()
    {
        // Arrange & Act
        var excepcion = Record.Exception(() => new RegistroDiario(-1, DateTime.UtcNow, "paginas"));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_02_RegistroDiario_FechaNoUtc_Rechaza()
    {
        // Arrange
        var fechaLocal = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);

        // Act
        var excepcion = Record.Exception(() => new RegistroDiario(5, fechaLocal, "paginas"));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Contains("UTC", excepcion.Message);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_RegistroDiario_UnidadVacia_Rechaza()
    {
        // Arrange & Act
        var excepcion = Record.Exception(() => new RegistroDiario(5, DateTime.UtcNow, ""));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
    }
}
