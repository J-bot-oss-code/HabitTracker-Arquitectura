using Permissions.Domain.Entities;

namespace Permissions.Application.Interfaces;

public interface ISolicitudPermisoRepository
{
    Task AddAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default);

    Task UpdateAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default);

    Task<SolicitudPermiso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermiso>> GetPendingAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermiso>> GetBySolicitanteAsync(
        string solicitante,
        bool soloPendientes,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermiso>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Borra definitivamente las solicitudes de ese solicitante (baja de cuenta). Idempotente. Devuelve cuántas borró.</summary>
    Task<int> EliminarPorSolicitanteAsync(string solicitante, CancellationToken cancellationToken = default);

    /// <summary>Hay una solicitud de esa persona y ese permiso Pendiente o Aplicada (sin distinguir mayúsculas).</summary>
    Task<bool> ExisteVigenteAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default);

    Task<bool> TienePermisoAplicadoAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default);
}
