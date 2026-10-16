using Permissions.Domain.Entities;
using Permissions.Domain.Enums;
using Permissions.Domain.Results;

namespace Permissions.Domain.Tests;

public class SolicitudPermisoTests
{
    private static readonly DateTime FechaResolucionUtc =
        new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_CreaSolicitudPendienteConIdentificadorYFechaUtc()
    {
        var solicitud = new SolicitudPermiso("persona@ejemplo.com", "Acceso a reportes");

        Assert.NotEqual(Guid.Empty, solicitud.Id);
        Assert.Equal("persona@ejemplo.com", solicitud.Solicitante);
        Assert.Equal("Acceso a reportes", solicitud.PermisoSolicitado);
        Assert.Equal(EstadoSolicitudPermiso.Pendiente, solicitud.Estado);
        Assert.Equal(DateTimeKind.Utc, solicitud.FechaCreacion.Kind);
        Assert.Null(solicitud.Aprobador);
        Assert.Null(solicitud.FechaResolucion);
        Assert.Null(solicitud.MotivoRechazo);
    }

    [Fact]
    public void Aprobar_AplicaSolicitudYRegistraResolucion()
    {
        var solicitud = CrearSolicitud();

        var resultado = solicitud.Aprobar("admin@ejemplo.com", FechaResolucionUtc);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(DomainError.Ninguno, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal("admin@ejemplo.com", solicitud.Aprobador);
        Assert.Equal(FechaResolucionUtc, solicitud.FechaResolucion);
        Assert.Null(solicitud.MotivoRechazo);
    }

    [Fact]
    public void Aprobar_SolicitanteNoPuedeResolverSuPropiaSolicitud()
    {
        var solicitud = CrearSolicitud();

        var resultado = solicitud.Aprobar("persona@ejemplo.com", FechaResolucionUtc);

        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.AutoResolucionProhibida);
    }

    [Fact]
    public void Aprobar_SolicitudAplicadaNoPuedeAprobarseOtraVez()
    {
        var solicitud = CrearSolicitud();
        Assert.True(solicitud.Aprobar("admin@ejemplo.com", FechaResolucionUtc).IsSuccess);
        var aprobadorOriginal = solicitud.Aprobador;
        var fechaOriginal = solicitud.FechaResolucion;

        var resultado = solicitud.Aprobar("otro-admin@ejemplo.com", FechaResolucionUtc.AddMinutes(1));

        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.EstadoTransicionInvalido, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal(aprobadorOriginal, solicitud.Aprobador);
        Assert.Equal(fechaOriginal, solicitud.FechaResolucion);
    }

    [Fact]
    public void Aprobar_SolicitudRechazadaNoPuedeAprobarse()
    {
        var solicitud = CrearSolicitud();
        Assert.True(solicitud.Rechazar("admin@ejemplo.com", "Motivo", FechaResolucionUtc).IsSuccess);

        var resultado = solicitud.Aprobar("otro-admin@ejemplo.com", FechaResolucionUtc.AddMinutes(1));

        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.EstadoTransicionInvalido, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Equal("admin@ejemplo.com", solicitud.Aprobador);
        Assert.Equal("Motivo", solicitud.MotivoRechazo);
        Assert.Equal(FechaResolucionUtc, solicitud.FechaResolucion);
    }

    [Fact]
    public void Rechazar_ConMotivoRegistraResolucionYEstadoRechazado()
    {
        var solicitud = CrearSolicitud();

        var resultado = solicitud.Rechazar("admin@ejemplo.com", "No cumple la política", FechaResolucionUtc);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Equal("admin@ejemplo.com", solicitud.Aprobador);
        Assert.Equal("No cumple la política", solicitud.MotivoRechazo);
        Assert.Equal(FechaResolucionUtc, solicitud.FechaResolucion);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rechazar_SinMotivoRechazaSinModificarSolicitud(string? motivo)
    {
        var solicitud = CrearSolicitud();

        var resultado = solicitud.Rechazar("admin@ejemplo.com", motivo!, FechaResolucionUtc);

        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.MotivoRechazoRequerido);
    }

    [Fact]
    public void Rechazar_SolicitanteNoPuedeResolverSuPropiaSolicitud()
    {
        var solicitud = CrearSolicitud();

        var resultado = solicitud.Rechazar("persona@ejemplo.com", "Motivo", FechaResolucionUtc);

        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.AutoResolucionProhibida);
    }

    [Fact]
    public void Rechazar_SolicitudRechazadaNoPuedeCambiarDeEstado()
    {
        var solicitud = CrearSolicitud();
        Assert.True(solicitud.Rechazar("admin@ejemplo.com", "Motivo inicial", FechaResolucionUtc).IsSuccess);
        var aprobadorOriginal = solicitud.Aprobador;
        var fechaOriginal = solicitud.FechaResolucion;
        var motivoOriginal = solicitud.MotivoRechazo;

        var resultado = solicitud.Rechazar("otro-admin@ejemplo.com", "Otro motivo", FechaResolucionUtc.AddMinutes(1));

        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.EstadoTransicionInvalido, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Rechazada, solicitud.Estado);
        Assert.Equal(aprobadorOriginal, solicitud.Aprobador);
        Assert.Equal(fechaOriginal, solicitud.FechaResolucion);
        Assert.Equal(motivoOriginal, solicitud.MotivoRechazo);
    }

    [Fact]
    public void Rechazar_SolicitudAplicadaNoPuedeRechazarse()
    {
        var solicitud = CrearSolicitud();
        Assert.True(solicitud.Aprobar("admin@ejemplo.com", FechaResolucionUtc).IsSuccess);

        var resultado = solicitud.Rechazar("otro-admin@ejemplo.com", "Motivo", FechaResolucionUtc.AddMinutes(1));

        Assert.False(resultado.IsSuccess);
        Assert.Equal(DomainError.EstadoTransicionInvalido, resultado.Error);
        Assert.Equal(EstadoSolicitudPermiso.Aplicada, solicitud.Estado);
        Assert.Equal("admin@ejemplo.com", solicitud.Aprobador);
        Assert.Null(solicitud.MotivoRechazo);
        Assert.Equal(FechaResolucionUtc, solicitud.FechaResolucion);
    }

    [Fact]
    public void Resolver_ConFechaNoUtcRechazaSinModificarSolicitud()
    {
        var solicitud = CrearSolicitud();
        var fechaLocal = DateTime.SpecifyKind(FechaResolucionUtc, DateTimeKind.Local);

        var resultado = solicitud.Aprobar("admin@ejemplo.com", fechaLocal);

        AssertRejectedWithoutMutation(solicitud, resultado, DomainError.FechaResolucionDebeSerUtc);
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
