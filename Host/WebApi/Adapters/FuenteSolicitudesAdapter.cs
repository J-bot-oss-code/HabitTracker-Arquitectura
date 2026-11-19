using Permissions.Application.Interfaces;
using Reports.Application.DTOs;
using Reports.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="IFuenteSolicitudes"/> de Reportes hacia las solicitudes de Permisos. Código de composición del Host: Reportes
/// no conoce a Permisos ni lee su tabla (RD-03); el GROUP BY lo hace la base de datos de Permisos.
/// </summary>
public sealed class FuenteSolicitudesAdapter(ISolicitudPermisoReportes permisos) : IFuenteSolicitudes
{
    public async Task<IReadOnlyList<FilaPermisosDto>> ContarPorEstadoAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, string? solicitante, CancellationToken cancellationToken = default) =>
        [.. (await permisos.ContarPorEstadoAsync(desdeUtc, hastaUtcExclusivo, solicitante, cancellationToken))
            .Select(c => new FilaPermisosDto(c.Estado, c.Cantidad))];
}
