using HabitTracker.Domain.Entities;

namespace HabitTracker.Domain.Tests;

public class HabitoTests
{
    private readonly Guid propietario = Guid.NewGuid();
    private readonly Guid noPropietario = Guid.NewGuid();
    private readonly Guid metaId = Guid.NewGuid();

    private Habito CrearHabito(EstadoHabito estado = EstadoHabito.Planificado)
    {
        var habito = new Habito(propietario, "Leer 20 paginas", Periodicidad.Diaria, 20, "paginas", metaId);

        // Forzar estado para pruebas de transicion
        var campoEstado = typeof(Habito).GetProperty("Estado", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        campoEstado!.SetValue(habito, estado);
        return habito;
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_04_Activar_Propietario_CambiaAActivo()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Planificado);

        // Act
        habito.Activar(propietario);

        // Assert
        Assert.Equal(EstadoHabito.Activo, habito.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_04_Activar_NoPropietario_Rechaza()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Planificado);

        // Act
        var excepcion = Record.Exception(() => habito.Activar(noPropietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
        Assert.Equal(EstadoHabito.Planificado, habito.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_04_Pausar_Propietario_CambiaAPausado()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Activo);

        // Act
        habito.Pausar(propietario);

        // Assert
        Assert.Equal(EstadoHabito.Pausado, habito.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-04")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_04_Pausar_NoPropietario_Rechaza()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Activo);

        // Act
        var excepcion = Record.Exception(() => habito.Pausar(noPropietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
        Assert.Equal(EstadoHabito.Activo, habito.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-05")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_05_Completar_PropietarioConMetaCumplida_CambiaACompletado()
    {
        // Arrange
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        habito.Activar(propietario);
        habito.RegistrarAvance(propietario, 6, DateTime.UtcNow);
        habito.RegistrarAvance(propietario, 5, DateTime.UtcNow);

        // Act
        habito.Completar(propietario);

        // Assert
        Assert.Equal(EstadoHabito.Completado, habito.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-05")]
    public void RF_NEG_05_Completar_SinMetaCumplida_Rechaza()
    {
        // Arrange
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        habito.Activar(propietario);
        habito.RegistrarAvance(propietario, 3, DateTime.UtcNow);

        // Act
        var excepcion = Record.Exception(() => habito.Completar(propietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains("prohibida", excepcion.Message);
        Assert.Equal(EstadoHabito.Activo, habito.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-05")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_05_Completar_NoPropietario_Rechaza()
    {
        // Arrange
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        habito.Activar(propietario);
        habito.RegistrarAvance(propietario, 15, DateTime.UtcNow);

        // Act
        var excepcion = Record.Exception(() => habito.Completar(noPropietario));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
        Assert.Equal(EstadoHabito.Activo, habito.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-05")]
    public void RF_NEG_02_Reconfigurar_CambiaPeriodicidadCantidadUnidad_NoCambiaEstado()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Activo);
        var estadoOriginal = habito.Estado;

        // Act
        habito.Reconfigurar(propietario, Periodicidad.Semanal, 100, "minutos");

        // Assert
        Assert.Equal(Periodicidad.Semanal, habito.Periodicidad);
        Assert.Equal(100m, habito.CantidadObjetivo);
        Assert.Equal("minutos", habito.Unidad);
        Assert.Equal(estadoOriginal, habito.Estado);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-05")]
    public void RF_NEG_02_Reconfigurar_HabitoCompletado_Rechaza()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Completado);

        // Act
        var excepcion = Record.Exception(() => habito.Reconfigurar(propietario, Periodicidad.Semanal, 100, "minutos"));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains("completado", excepcion.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-06")]
    public void RF_NEG_02_Reconfigurar_NoPropietario_Rechaza()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Activo);

        // Act
        var excepcion = Record.Exception(() => habito.Reconfigurar(noPropietario, Periodicidad.Semanal, 100, "minutos"));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<UnauthorizedAccessException>(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_02_RegistrarAvance_ActivoYPropietario_AgregaRegistroConFechaUtc()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Activo);
        var fechaUtc = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Utc);

        // Act
        habito.RegistrarAvance(propietario, 5, fechaUtc);

        // Assert
        Assert.Single(habito.Registros);
        var registro = Assert.Single(habito.Registros);
        Assert.Equal(5, registro.Cantidad);
        Assert.Equal(DateTimeKind.Utc, registro.Fecha.Kind);
        Assert.Equal("paginas", registro.Unidad);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_RegistrarAvide_HabitoNoActivo_Rechaza()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Planificado);

        // Act
        var excepcion = Record.Exception(() => habito.RegistrarAvance(propietario, 5, DateTime.UtcNow));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<InvalidOperationException>(excepcion);
        Assert.Contains("Activo", excepcion.Message);
        Assert.Empty(habito.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_RegistrarAvide_CantidadCeroONegativa_Rechaza()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Activo);

        // Act
        var excepcion = Record.Exception(() => habito.RegistrarAvance(propietario, 0, DateTime.UtcNow));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Empty(habito.Registros);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_02_RegistrarAvide_FechaNoUtc_Rechaza()
    {
        // Arrange
        var habito = CrearHabito(EstadoHabito.Activo);
        var fechaLocal = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Local);

        // Act
        var excepcion = Record.Exception(() => habito.RegistrarAvance(propietario, 5, fechaLocal));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
        Assert.Contains("UTC", excepcion.Message);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Constructor_EstadoInicialEsPlanificado()
    {
        // Arrange & Act
        var habito = new Habito(propietario, "Correr", Periodicidad.Diaria, 5, "km", metaId);

        // Assert
        Assert.Equal(EstadoHabito.Planificado, habito.Estado);
        Assert.Equal(propietario, habito.UsuarioId);
        Assert.Equal(Periodicidad.Diaria, habito.Periodicidad);
        Assert.Equal(5m, habito.CantidadObjetivo);
        Assert.Equal("km", habito.Unidad);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public void RF_NEG_02_Constructor_CantidadObjetivoCero_Rechaza()
    {
        // Arrange & Act
        var excepcion = Record.Exception(() => new Habito(propietario, "Test", Periodicidad.Diaria, 0, "unidades", metaId));

        // Assert
        Assert.NotNull(excepcion);
        Assert.IsType<ArgumentException>(excepcion);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-05")]
    public void RF_NEG_02_Reconfigurar_ConservaRegistrosHistoricos()
    {
        // Arrange
        var hoy = DateTime.UtcNow.Date;
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        habito.Activar(propietario);
        habito.RegistrarAvance(propietario, 3, new DateTime(hoy.Year, hoy.Month, hoy.Day, 8, 0, 0, DateTimeKind.Utc));
        habito.RegistrarAvance(propietario, 4, new DateTime(hoy.Year, hoy.Month, hoy.Day, 18, 0, 0, DateTimeKind.Utc));

        // Act
        habito.Reconfigurar(propietario, Periodicidad.Semanal, 20, "minutos");

        // Assert
        Assert.Equal(2, habito.Registros.Count);
        Assert.Equal(Periodicidad.Semanal, habito.Periodicidad);
        Assert.Equal(20m, habito.CantidadObjetivo);
        Assert.Equal("minutos", habito.Unidad);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_02_Reconfigurar_IniciaNuevoCiclo_RegistrosAnterioresNoCuentan()
    {
        // Arrange
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        habito.Activar(propietario);
        // Dos registros antes del ciclo (ya cumplen la meta antigua)
        habito.RegistrarAvance(propietario, 6, DateTime.UtcNow);
        habito.RegistrarAvance(propietario, 6, DateTime.UtcNow);

        // Act - Reconfigurar con nueva meta más alta
        habito.Reconfigurar(propietario, Periodicidad.Diaria, 15, "paginas");

        // Assert - Los registros anteriores no cuentan para la nueva meta (ciclo reiniciado)
        Assert.False(habito.CumpleMetaFinal);
        Assert.Equal(2, habito.Registros.Count); // Historial conservado
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_02_Reconfigurar_SoloRegistrosDespuesDeCicloCuentanParaNuevaMeta()
    {
        // Arrange
        var hoy = DateTime.UtcNow.Date;
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 10, "paginas", metaId);
        habito.Activar(propietario);
        habito.RegistrarAvance(propietario, 10, new DateTime(hoy.Year, hoy.Month, hoy.Day, 8, 0, 0, DateTimeKind.Utc));

        // Act - Reconfigurar (inicia nuevo ciclo en FechaInicioCiclo = ahora)
        habito.Reconfigurar(propietario, Periodicidad.Diaria, 5, "paginas");
        // Registrar avance DESPUÉS del reconfigurar
        habito.RegistrarAvance(propietario, 6, DateTime.UtcNow);

        // Assert - Solo el avance después del ciclo cuenta
        Assert.True(habito.CumpleMetaFinal);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_02_Reconfigurar_RegistroAntesDeCicloNoCuentaAunqueCumplaCantidad()
    {
        // Arrange
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 100, "paginas", metaId);
        habito.Activar(propietario);
        // Registro grande pero ANTES del reconfigurar
        habito.RegistrarAvance(propietario, 100, DateTime.UtcNow.AddMinutes(-1));

        // Act - Reconfigurar con meta baja (5)
        habito.Reconfigurar(propietario, Periodicidad.Diaria, 5, "paginas");

        // Assert - El registro anterior no cuenta para la nueva meta
        Assert.False(habito.CumpleMetaFinal);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Diaria_LimiteUTC_RegistroAlInicioDelDiaCuenta()
    {
        // Arrange
        var hoy = DateTime.UtcNow.Date;
        var inicioDia = new DateTime(hoy.Year, hoy.Month, hoy.Day, 0, 0, 0, DateTimeKind.Utc);
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 5, "paginas", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, inicioDia);
        habito.RegistrarAvance(propietario, 5, inicioDia);

        // Act
        var cumple = habito.CumpleMetaFinal;

        // Assert
        Assert.True(cumple);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Diaria_LimiteUTC_RegistroAlFinalDelDiaCuenta()
    {
        // Arrange
        var hoy = DateTime.UtcNow.Date;
        var finDia = new DateTime(hoy.Year, hoy.Month, hoy.Day, 23, 59, 59, DateTimeKind.Utc);
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 5, "paginas", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, hoy);

        // Act
        habito.RegistrarAvance(propietario, 5, finDia);
        var cumple = habito.CumpleMetaFinal;

        // Assert
        Assert.True(cumple);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Diaria_LimiteUTC_RegistroDelDiaSiguienteNoCuenta()
    {
        // Arrange
        var hoy = DateTime.UtcNow.Date;
        var manana = hoy.AddDays(1);
        var habito = new Habito(propietario, "Leer", Periodicidad.Diaria, 5, "paginas", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, hoy);

        // Act
        habito.RegistrarAvance(propietario, 5, new DateTime(manana.Year, manana.Month, manana.Day, 8, 0, 0, DateTimeKind.Utc));
        var cumple = habito.CumpleMetaFinal;

        // Assert
        Assert.False(cumple);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-03")]
    [Trait("Requirement", "RD-11")]
    public void RF_NEG_03_CalculoMeta_Semanal_LimiteUTC_LunesEsInicioDeSemana()
    {
        // Arrange
        var referencia = DateTime.UtcNow;
        var lunes = ObtenerLunesUTC(referencia);
        var habito = new Habito(propietario, "Correr", Periodicidad.Semanal, 10, "km", metaId);
        habito.Activar(propietario);
        EstablecerFechaInicioCiclo(habito, lunes);
        habito.RegistrarAvance(propietario, 10, new DateTime(lunes.Year, lunes.Month, lunes.Day, 0, 0, 0, DateTimeKind.Utc));

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
