using Permissions.Domain.Entities;
using Permissions.Domain.Enums;
using Permissions.Domain.Results;

namespace Permissions.Domain.Tests;

public class SolicitudPermisoTests
{
    private static readonly DateTime FechaResolucionUtc =
        new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("Requirement", "RF-GP-01")]
    public void RF_GP_01_Constructor_CreaSolicitudPendienteConIdentificadorYFechaUtc()
    {
        // Arrange
        const string solicitante = "persona@ejemplo.com";
        const string permiso = "Acceso a reportes";

        // Act
        var solicitud = new SolicitudPermiso(solicitante, permiso);

        // Assert
        Assert.NotEqual(Guid.Empty, solicitud.Id);
        Assert.Equal(solicitante, solicitud.Solicitante);
        Assert.Equal(permiso, solicitud.PermisoSolicitado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Equal(DateTimeKind.Utc, solicitud.FechaCreacion.Kind);
        Assert.Null(solicitud.Aprobador);
        Assert.Null(solicitud.FechaResolucion);
        Assert.Null(solicitud.MotivoRechazo);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-01")]
    public void RF_GP_01_Constructor_RechazaCamposQueSuperanLaLongitudPermitida()
    {
        // Arrange
        var solicitanteExcedido = new string('a', 257);
        var permisoExcedido = new string('p', 257);

        // Act
        var errorSolicitante = Record.Exception(() => new SolicitudPermiso(solicitanteExcedido, "Permiso"));
        var errorPermiso = Record.Exception(() => new SolicitudPermiso("persona@ejemplo.com", permisoExcedido));

        // Assert
        Assert.IsType<ArgumentException>(errorSolicitante);
        Assert.IsType<ArgumentException>(errorPermiso);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-03")]
    [Trait("Requirement", "RF-GP-05")]
    public void RF_GP_03_05_Aprobar_AplicaSolicitudYRegistraResolucion()
    {
        // Arrange
        var solicitud = CrearSolicitud();

        // Act
        var resultado = solicitud.Aprobar("admin@ejemplo.com", FechaResolucionUtc);

        // Assert
        Assert.True(resultado.IsSuccess);
        Assert.Equal(DomainError.Ninguno, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal("admin@ejemplo.com", solicitud.Aprobador);
        Assert.Equal(FechaResolucionUtc, solicitud.FechaResolucion);
        Assert.Null(solicitud.MotivoRechazo);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-07")]
    public void RF_GP_07_Aprobar_SolicitanteEsMismoQueAutorizador_Rechaza()
    {
        // Arrange
        var solicitud = CrearSolicitud();

        // Act - El solicitante intenta aprobar su propia solicitud
        var resultado = solicitud.Aprobar("persona@ejemplo.com", FechaResolucionUtc);

        // Assert
        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.AutoResolucionProhibida);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-06")]
    public void RF_GP_06_Aprobar_SolicitudAplicadaNoPuedeAprobarseOtraVez()
    {
        // Arrange
        var solicitud = CrearSolicitud();
        solicitud.Aprobar("admin@ejemplo.com", FechaResolucionUtc);
        var aprobadorOriginal = solicitud.Aprobador;
        var fechaOriginal = solicitud.FechaResolucion;

        // Act
        var resultado = solicitud.Aprobar("otro-admin@ejemplo.com", FechaResolucionUtc.AddMinutes(1));

        // Assert
        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.EstadoTransicionInvalido, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal(aprobadorOriginal, solicitud.Aprobador);
        Assert.Equal(fechaOriginal, solicitud.FechaResolucion);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-06")]
    public void RF_GP_06_Aprobar_SolicitudRechazadaNoPuedeAprobarse()
    {
        // Arrange
        var solicitud = CrearSolicitud();
        solicitud.Rechazar("admin@ejemplo.com", "Motivo inicial", FechaResolucionUtc);

        // Act - Intento de aprobar una solicitud ya rechazada
        var resultado = solicitud.Aprobar("otro-admin@ejemplo.com", FechaResolucionUtc.AddMinutes(1));

        // Assert
        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.EstadoTransicionInvalido, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Equal("admin@ejemplo.com", solicitud.Aprobador);
        Assert.Equal("Motivo inicial", solicitud.MotivoRechazo);
        Assert.Equal(FechaResolucionUtc, solicitud.FechaResolucion);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-04")]
    public void RF_GP_04_Rechazar_ConMotivoRegistraResolucionYEstadoRechazado()
    {
        // Arrange
        var solicitud = CrearSolicitud();

        // Act
        var resultado = solicitud.Rechazar("admin@ejemplo.com", "No cumple la política", FechaResolucionUtc);

        // Assert
        Assert.True(resultado.IsSuccess);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Equal("admin@ejemplo.com", solicitud.Aprobador);
        Assert.Equal("No cumple la política", solicitud.MotivoRechazo);
        Assert.Equal(FechaResolucionUtc, solicitud.FechaResolucion);
    }

    [Theory]
    [Trait("Requirement", "RF-GP-04")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RF_GP_04_Rechazar_SinMotivoRechazaSinModificarSolicitud(string? motivo)
    {
        // Arrange
        var solicitud = CrearSolicitud();

        // Act
        var resultado = solicitud.Rechazar("admin@ejemplo.com", motivo!, FechaResolucionUtc);

        // Assert
        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.MotivoRechazoRequerido);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-07")]
    public void RF_GP_07_Rechazar_SolicitanteEsMismoQueAutorizador_Rechaza()
    {
        // Arrange
        var solicitud = CrearSolicitud();

        // Act - El solicitante intenta rechazar su propia solicitud (auto-resolución)
        var resultado = solicitud.Rechazar("persona@ejemplo.com", "Motivo", FechaResolucionUtc);

        // Assert
        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.AutoResolucionProhibida);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-04")]
    public void RF_GP_04_Rechazar_MotivoDemasiadoLargoRechazaSinModificarSolicitud()
    {
        // Arrange
        var solicitud = CrearSolicitud();

        // Act
        var resultado = solicitud.Rechazar("admin@ejemplo.com", new string('m', 501), FechaResolucionUtc);

        // Assert
        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.MotivoRechazoExcedeLongitudMaxima);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-06")]
    public void RF_GP_06_Rechazar_SolicitudRechazadaNoPuedeCambiarDeEstado()
    {
        // Arrange
        var solicitud = CrearSolicitud();
        solicitud.Rechazar("admin@ejemplo.com", "Motivo inicial", FechaResolucionUtc);
        var aprobadorOriginal = solicitud.Aprobador;
        var fechaOriginal = solicitud.FechaResolucion;
        var motivoOriginal = solicitud.MotivoRechazo;

        // Act
        var resultado = solicitud.Rechazar("otro-admin@ejemplo.com", "Otro motivo", FechaResolucionUtc.AddMinutes(1));

        // Assert
        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.EstadoTransicionInvalido, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Equal(aprobadorOriginal, solicitud.Aprobador);
        Assert.Equal(fechaOriginal, solicitud.FechaResolucion);
        Assert.Equal(motivoOriginal, solicitud.MotivoRechazo);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-06")]
    public void RF_GP_06_Rechazar_SolicitudAplicadaNoPuedeRechazarse()
    {
        // Arrange
        var solicitud = CrearSolicitud();
        solicitud.Aprobar("admin@ejemplo.com", FechaResolucionUtc);

        // Act
        var resultado = solicitud.Rechazar("otro-admin@ejemplo.com", "Motivo", FechaResolucionUtc.AddMinutes(1));

        // Assert
        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.EstadoTransicionInvalido, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal("admin@ejemplo.com", solicitud.Aprobador);
        Assert.Null(solicitud.MotivoRechazo);
        Assert.Equal(FechaResolucionUtc, solicitud.FechaResolucion);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-06")]
    public void RF_GP_06_Resolver_ConFechaNoUtcRechazaSinModificarSolicitud()
    {
        // Arrange
        var solicitud = CrearSolicitud();
        var fechaLocal = DateTime.SpecifyKind(FechaResolucionUtc, DateTimeKind.Local);

        // Act
        var resultado = solicitud.Aprobar("admin@ejemplo.com", fechaLocal);

        // Assert
        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.FechaResolucionDebeSerUtc);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-06")]
    public void RF_GP_06_Transicion_AprobadaNoPuedeVolverAPendiente()
    {
        // Arrange
        var estadoActual = EstadoSolicitudPermiso.Aprobada;
        var nuevoEstado = EstadoSolicitudPermiso.Pendiente;

        // Act
        var transicionValida = TransicionesSolicitudPermiso.EsValida(estadoActual, nuevoEstado);

        // Assert
        Assert.False(transicionValida);
    }

    [Fact]
    [Trait("Requirement", "RF-GP-06")]
    public void RF_GP_06_Aprobar_AprobadorQueSuperaLongitudMaximaRechazaSinModificarSolicitud()
    {
        // Arrange
        var solicitud = CrearSolicitud();
        var aprobadorExcedido = new string('a', 257);

        // Act
        var resultado = solicitud.Aprobar(aprobadorExcedido, FechaResolucionUtc);

        // Assert
        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.AprobadorExcedeLongitudMaxima);
    }

    private static SolicitudPermiso CrearSolicitud() =>
        new("persona@ejemplo.com", "Acceso a reportes");

    private static void AssertRejectedWithoutMutation(
        SolicitudPermiso solicitud,
        DomainResult resultado,
        DomainError errorEsperado)
    {
        Assert.False(resultado.IsSuccess);
        Assert.Equal(errorEsperado, resultado.Error);
        Assert.NotNull(resultado.ErrorMessage);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Null(solicitud.Aprobador);
        Assert.Null(solicitud.FechaResolucion);
        Assert.Null(solicitud.MotivoRechazo);
    }
}
