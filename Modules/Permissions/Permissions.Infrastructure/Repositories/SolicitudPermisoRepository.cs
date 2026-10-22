using Microsoft.EntityFrameworkCore;
using Permissions.Application.Interfaces;
using Permissions.Domain.Entities;
using Permissions.Domain.Enums;
using Permissions.Infrastructure.Persistence;

namespace Permissions.Infrastructure.Repositories;

public sealed class SolicitudPermisoRepository(PermissionsDbContext dbContext) : ISolicitudPermisoRepository
{
    public async Task AddAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        await dbContext.SolicitudesPermisos.AddAsync(solicitud, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SolicitudPermiso solicitud, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(solicitud);
        dbContext.SolicitudesPermisos.Update(solicitud);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<SolicitudPermiso?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.SolicitudesPermisos
            .AsNoTracking()
            .FirstOrDefaultAsync(solicitud => solicitud.Id == id, cancellationToken);

    public async Task<IReadOnlyList<SolicitudPermiso>> GetPendingAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SolicitudesPermisos
            .AsNoTracking()
            .Where(solicitud => solicitud.Estado == EstadoSolicitudPermiso.Pendiente)
            .OrderBy(solicitud => solicitud.FechaCreacion)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SolicitudPermiso>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.SolicitudesPermisos
            .AsNoTracking()
            .OrderBy(solicitud => solicitud.FechaCreacion)
            .ToListAsync(cancellationToken);

    public Task<bool> TienePermisoAplicadoAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default) =>
        dbContext.SolicitudesPermisos
            .AsNoTracking()
            .AnyAsync(solicitud =>
                solicitud.Estado == EstadoSolicitudPermiso.Aplicada &&
                solicitud.Solicitante.ToUpper() == solicitante.ToUpper() &&
                solicitud.PermisoSolicitado.ToUpper() == permisoSolicitado.ToUpper(),
                cancellationToken);

    public async Task<IReadOnlyList<SolicitudPermiso>> GetBySolicitanteAsync(
        string solicitante,
        CancellationToken cancellationToken = default) =>
        await dbContext.SolicitudesPermisos
            .AsNoTracking()
            .Where(solicitud => solicitud.Solicitante.ToUpper() == solicitante.ToUpper())
            .OrderBy(solicitud => solicitud.FechaCreacion)
            .ToListAsync(cancellationToken);
}
