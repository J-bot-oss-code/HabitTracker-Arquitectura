using HabitTracker.Domain.Entities;

namespace HabitTracker.Domain.Tests;

public class CalculoMetaTests
{
    private readonly Guid propietario = Guid.NewGuid();
    private readonly Guid metaId = Guid.NewGuid();

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Diaria_SumaDelDiaUTC_Alcanzada()
    {
        // Arrange
        var hoy = DateTime.UtcNow.Date;
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, hoy);
        habito.RegistrarAvance(propietario, 6, new DateTime(hoy.Year, hoy.Month, hoy.Day, 8, 0, 0, DateTimeKind.Utc));
        habito.RegistrarAvance(propietario, 5, new DateTime(hoy.Year, hoy.Month, hoy.Day, 18, 0, 0, DateTimeKind.Utc));

        // Act
        var cumple = habito.CumpleMetaFinal;

        // Assert
        Assert.True(cumple);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Diaria_SumaDelDiaUTC_NoAlcanzada()
    {
        // Arrange
        var hoy = DateTime.UtcNow.Date;
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, hoy);
        habito.RegistrarAvance(propietario, 3, new DateTime(hoy.Year, hoy.Month, hoy.Day, 8, 0, 0, DateTimeKind.Utc));

        // Act
        var cumple = habito.CumpleMetaFinal;

        // Assert
        Assert.False(cumple);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Diaria_RegistroDeAyerNoCuenta()
    {
        // Arrange
        var hoy = DateTime.UtcNow.Date;
        var ayer = hoy.AddDays(-1);
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, hoy);
        habito.RegistrarAvance(propietario, 15, new DateTime(ayer.Year, ayer.Month, ayer.Day, 8, 0, 0, DateTimeKind.Utc));

        // Act
        var cumple = habito.CumpleMetaFinal;

        // Assert
        Assert.False(cumple);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Semanal_SumaSemanaISO_LunesALunesUTC_Alcanzada()
    {
        // Arrange
        var referencia = DateTime.UtcNow;
        var inicioSemana = ObtenerLunesUTC(referencia);
        var habito = new Habito(propietario, "Correr", Periodicidad.Semanal, 30, "km", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, inicioSemana);
        habito.RegistrarAvance(propietario, 10, new DateTime(inicioSemana.Year, inicioSemana.Month, inicioSemana.Day, 8, 0, 0, DateTimeKind.Utc));
        habito.RegistrarAvance(propietario, 12, inicioSemana.AddDays(3));
        habito.RegistrarAvance(propietario, 8, inicioSemana.AddDays(6));

        // Act
        var cumple = habito.CumpleMetaFinal;

        // Assert
        Assert.True(cumple);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Semanal_RegistroDeSemanaAnteriorNoCuenta()
    {
        // Arrange
        var referencia = DateTime.UtcNow;
        var inicioSemana = ObtenerLunesUTC(referencia);
        var semanaAnterior = inicioSemana.AddDays(-7);
        var habito = new Habito(propietario, "Correr", Periodicidad.Semanal, 30, "km", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, inicioSemana);
        habito.RegistrarAvance(propietario, 50, new DateTime(semanaAnterior.Year, semanaAnterior.Month, semanaAnterior.Day, 8, 0, 0, DateTimeKind.Utc));

        // Act
        var cumple = habito.CumpleMetaFinal;

        // Assert
        Assert.False(cumple);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Semanal_DomingoPerteneceASemanaActualISO()
    {
        // Arrange
        var referencia = DateTime.UtcNow;
        var inicioSemana = ObtenerLunesUTC(referencia);
        var domingo = inicioSemana.AddDays(6);
        var habito = new Habito(propietario, "Correr", Periodicidad.Semanal, 5, "km", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, inicioSemana);
        habito.RegistrarAvance(propietario, 5, new DateTime(domingo.Year, domingo.Month, domingo.Day, 8, 0, 0, DateTimeKind.Utc));

        // Act
        var cumple = habito.CumpleMetaFinal;

        // Assert
        Assert.True(cumple);
    }

    private static DateTime ObtenerLunesUTC(DateTime fechaUtc)
    {
        var diff = fechaUtc.DayOfWeek - DayOfWeek.Monday;
        if (diff < 0) diff += 7;
        return fechaUtc.Date.AddDays(-diff);
    }

    private static void EstablecerFechaInicioCiclo(Habito habito, DateTime fechaInicio)
    {
        var campo = typeof(Habito).GetProperty("FechaInicioCiclo", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        campo!.SetValue(habito, fechaInicio);
    }
}
