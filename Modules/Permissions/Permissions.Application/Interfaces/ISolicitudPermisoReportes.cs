namespace Permissions.Application.Interfaces;

/// <summary>Cuántas solicitudes hay en un estado.</summary>
public sealed record ConteoPorEstado(string Estado, int Cantidad);

/// <summary>
/// Consulta agregada de solicitudes de permiso para el reporte del Core (RF-REP-01, 03 y 05). La agrupación la hace la base de
/// datos: devuelve una fila por estado, nunca el listado de solicitudes.
/// </summary>
public interface ISolicitudPermisoReportes
{
    /// <summary>
    /// Solicitudes creadas entre <paramref name="desdeUtc"/> (incluida) y <paramref name="hastaUtcExclusivo"/> (excluida), por estado,
    /// en el orden de la máquina (Pendiente, Aprobada, Aplicada, Rechazada); solo los estados que tienen alguna. Con
    /// <paramref name="solicitante"/> cuenta solo las de esa persona (sin distinguir mayúsculas); sin él, las de todas.
    /// </summary>
    Task<IReadOnlyList<ConteoPorEstado>> ContarPorEstadoAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, string? solicitante, CancellationToken cancellationToken = default);
}
