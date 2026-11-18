using Microsoft.EntityFrameworkCore;
using Permissions.Application.Interfaces;
using Permissions.Infrastructure.Persistence;

namespace Permissions.Infrastructure.Repositories;

public sealed class SolicitudPermisoReportes(PermissionsDbContext dbContext) : ISolicitudPermisoReportes
{
    public async Task<IReadOnlyList<ConteoPorEstado>> ContarPorEstadoAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, string? solicitante, CancellationToken cancellationToken = default)
    {
        var consulta = dbContext.SolicitudesPermisos
            .AsNoTracking()
            .Where(s => s.FechaCreacion >= desdeUtc && s.FechaCreacion < hastaUtcExclusivo);

        if (!string.IsNullOrWhiteSpace(solicitante))
        {
            var normalizado = solicitante.Trim().ToUpper();
            consulta = consulta.Where(s => s.Solicitante.ToUpper() == normalizado);
        }

        // GROUP BY en la base de datos: solo viajan las filas ya agregadas.
        var filas = await consulta
            .GroupBy(s => s.Estado)
            .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
            .ToListAsync(cancellationToken);

        return [.. filas.OrderBy(f => f.Estado).Select(f => new ConteoPorEstado(f.Estado.ToString(), f.Cantidad))];
    }
}
