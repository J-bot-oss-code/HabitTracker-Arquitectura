using Permissions.Domain.Enums;
using Permissions.Domain.Results;

namespace Permissions.Domain.Entities;

public class SolicitudPermiso
{
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

        if (EsAutoResolucion(aprobador))
        {
            return DomainResult.Failure(DomainError.AutoResolucionProhibida);
        }

        if (string.IsNullOrWhiteSpace(motivoRechazo))
        {
            return DomainResult.Failure(DomainError.MotivoRechazoRequerido);
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
