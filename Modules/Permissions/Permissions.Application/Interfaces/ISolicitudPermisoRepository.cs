using Permissions.Domain.Entities;

namespace Permissions.Application.Interfaces;

public interface ISolicitudPermisoRepository
{
    Task AddAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default);

    Task UpdateAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default);

    Task<SolicitudPermiso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermiso>> GetPendingAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermiso>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<bool> TienePermisoAplicadoAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermiso>> GetBySolicitanteAsync(
        string solicitante,
        CancellationToken cancellationToken = default);
}
