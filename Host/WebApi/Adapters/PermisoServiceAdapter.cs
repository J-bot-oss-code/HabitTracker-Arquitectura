using HabitTracker.Application.Interfaces;
using Permissions.Application.Interfaces;

namespace WebApi.Adapters;

public sealed class PermisoServiceAdapter(ISolicitudPermisoService solicitudPermisoService) : IPermisoService
{
    public Task<bool> TienePermisoEfectivoAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default) =>
        solicitudPermisoService.TienePermisoEfectivoAsync(solicitante, permisoSolicitado, cancellationToken);
}