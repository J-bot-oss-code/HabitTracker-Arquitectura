using Permissions.Domain.Enums;
using Permissions.Domain.Results;

namespace Permissions.Domain.Entities;

public class SolicitudPermiso
{
    private const int LongitudMaximaIdentidad = 256;
    private const int LongitudMaximaPermiso = 256;
    private const int LongitudMaximaMotivoRechazo = 500;

    public Guid Id { get; private set; }
    public string Solicitante { get; private set; } = string.Empty;
    public string PermisoSolicitado { get; private set; } = string.Empty;
    public EstadoSolicitudPermiso Estado { get; private set; }
    public string? Aprobador { get; private set; }
    public DateTime FechaCreacion { get; private set; }
    public DateTime? FechaResolucion { get; private set; }
    public string? MotivoRechazo { get; private set; }

    private SolicitudPermiso()
    {
    }

    public SolicitudPermiso(string solicitante, string permisoSolicitado)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(solicitante);
        ArgumentException.ThrowIfNullOrWhiteSpace(permisoSolicitado);
        ValidarLongitud(solicitante, LongitudMaximaIdentidad, nameof(solicitante));
        ValidarLongitud(permisoSolicitado, LongitudMaximaPermiso, nameof(permisoSolicitado));

        Id = Guid.NewGuid();
        Solicitante = solicitante.Trim();
        PermisoSolicitado = permisoSolicitado.Trim();
        Estado = EstadoSolicitudPermiso.Pendiente;
        FechaCreacion = DateTime.UtcNow;
    }

    public DomainResult Aprobar(string aprobador, DateTime fechaResolucionUtc)
    {
        if (string.IsNullOrWhiteSpace(aprobador))
        {
            return DomainResult.Failure(DomainError.AprobadorRequerido);
        }

        if (aprobador.Trim().Length > LongitudMaximaIdentidad)
        {
            return DomainResult.Failure(DomainError.AprobadorExcedeLongitudMaxima);
        }

        if (EsAutoResolucion(aprobador))
        {
            return DomainResult.Failure(DomainError.AutoResolucionProhibida);
        }

        if (fechaResolucionUtc.Kind != DateTimeKind.Utc)
        {
            return DomainResult.Failure(DomainError.FechaResolucionDebeSerUtc);
        }

        if (!TransicionarA(EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada))
        {
            return DomainResult.Failure(DomainError.EstadoTransicionInvalido);
        }

        Aprobador = aprobador.Trim();
        FechaResolucion = fechaResolucionUtc;
        MotivoRechazo = null;

        return DomainResult.Success();
    }

    public DomainResult Rechazar(string aprobador, string motivoRechazo, DateTime fechaResolucionUtc)
    {
        if (string.IsNullOrWhiteSpace(aprobador))
        {
            return DomainResult.Failure(DomainError.AprobadorRequerido);
        }

        if (aprobador.Trim().Length > LongitudMaximaIdentidad)
        {
            return DomainResult.Failure(DomainError.AprobadorExcedeLongitudMaxima);
        }

        if (EsAutoResolucion(aprobador))
        {
            return DomainResult.Failure(DomainError.AutoResolucionProhibida);
        }

        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            return DomainResult.Failure(DomainError.MotivoRechazoRequerido);
        }

        if (motivoRechazo.Trim().Length > LongitudMaximaMotivoRechazo)
        {
            return DomainResult.Failure(DomainError.MotivoRechazoExcedeLongitudMaxima);
        }

        if (fechaResolucionUtc.Kind != DateTimeKind.Utc)
        {
            return DomainResult.Failure(DomainError.FechaResolucionDebeSerUtc);
        }

        if (!TransicionarA(EstadoSolicitudPermiso.Rechazada))
        {
            return DomainResult.Failure(DomainError.EstadoTransicionInvalido);
        }

        Aprobador = aprobador.Trim();
        MotivoRechazo = motivoRechazo.Trim();
        FechaResolucion = fechaResolucionUtc;

        return DomainResult.Success();
    }

    private bool EsAutoResolucion(string aprobador) =>
        string.Equals(Solicitante, aprobador.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void ValidarLongitud(string valor, int longitudMaxima, string parametro)
    {
        if (valor.Trim().Length > longitudMaxima)
        {
            throw new ArgumentException($"El valor no puede superar {longitudMaxima} caracteres.", parametro);
        }
    }

    private bool TransicionarA(params EstadoSolicitudPermiso[] ruta)
    {
        if (ruta.Length == 0)
        {
            return false;
        }

        var estadoValidado = Estado;
        foreach (var nuevoEstado in ruta)
        {
            if (!TransicionesSolicitudPermiso.EsValida(estadoValidado, nuevoEstado))
            {
                return false;
            }

            estadoValidado = nuevoEstado;
        }

        foreach (var nuevoEstado in ruta)
        {
            Estado = nuevoEstado;
        }

        return true;
    }
}
