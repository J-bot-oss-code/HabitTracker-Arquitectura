using HabitTracker.Domain.Entities;

namespace HabitTracker.Domain.Tests;

public class TransicionesHabitoTests
{
    private readonly Guid propietario = Guid.NewGuid();

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    public void RF_NEG_04_Transicion_PlanificadoAAActivo_Permitida_ConPropietario()
    {
        // Arrange
        var actual = EstadoHabito.Planificado;
        var nuevo = EstadoHabito.Activo;

        // Act
        var valida = TransicionesHabito.EsTransicionValida(actual, nuevo);

        // Assert
        Assert.True(valida);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    public void RF_NEG_04_Transicion_ActivoAPausado_Permitida()
    {
        // Arrange
        var actual = EstadoHabito.Activo;
        var nuevo = EstadoHabito.Pausado;

        // Act
        var valida = TransicionesHabito.EsTransicionValida(actual, nuevo);

        // Assert
        Assert.True(valida);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    public void RF_NEG_04_Transicion_PausadoAActivo_Permitida()
    {
        // Arrange
        var actual = EstadoHabito.Pausado;
        var nuevo = EstadoHabito.Activo;

        // Act
        var valida = TransicionesHabito.EsTransicionValida(actual, nuevo);

        // Assert
        Assert.True(valida);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-05")]
    public void RF_NEG_05_Transicion_ActivoACompletado_PermitidaSoloSiCumpleMeta()
    {
        // Arrange
        var actual = EstadoHabito.Activo;
        var nuevo = EstadoHabito.Completado;

        // Act - Sin meta cumplida
        var sinMeta = TransicionesHabito.EsTransicionValida(actual, nuevo, cumpleMetaFinal: false);
        // Act - Con meta cumplida
        var conMeta = TransicionesHabito.EsTransicionValida(actual, nuevo, cumpleMetaFinal: true);

        // Assert
        Assert.False(sinMeta);
        Assert.True(conMeta);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    public void RF_NEG_04_Transicion_PlanificadoACompletado_Rechazada_YPermanecePlanificado()
    {
        // Arrange
        var actual = EstadoHabito.Planificado;
        var nuevo = EstadoHabito.Completado;

        // Act
        var excepcion = Record.Exception(() => TransicionesHabito.EsTransicionValida(actual, nuevo));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains("No se puede saltar el estado Activo", excepcion.Message);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-05")]
    public void RF_NEG_05_Transicion_PausadoACompletado_Rechazada_YPermanecePausado()
    {
        // Arrange
        var actual = EstadoHabito.Pausado;
        var nuevo = EstadoHabito.Completado;

        // Act
        var excepcion = Record.Exception(() => TransicionesHabito.EsTransicionValida(actual, nuevo));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains("Primero debe volver a Activo", excepcion.Message);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-05")]
    public void RF_NEG_05_Transicion_CompletadoAOtra_Rechazada_EstadoTerminal()
    {
        // Arrange
        var actual = EstadoHabito.Completado;
        var nuevo = EstadoHabito.Activo;

        // Act
        var excepcion = Record.Exception(() => TransicionesHabito.EsTransicionValida(actual, nuevo));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains("Completado es terminal", excepcion.Message);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    public void RF_NEG_04_CualquierTransicionNoListada_Rechazada()
    {
        // Arrange
        var actual = EstadoHabito.Planificado;
        var nuevo = EstadoHabito.Pausado; // No esta en la tabla permitida

        // Act
        var valida = TransicionesHabito.EsTransicionValida(actual, nuevo);

        // Assert
        Assert.False(valida);
    }
}
