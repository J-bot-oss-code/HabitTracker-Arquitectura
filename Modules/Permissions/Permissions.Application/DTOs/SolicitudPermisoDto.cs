namespace Permissions.Application.DTOs;

public sealed record SolicitudPermisoDto(
    Guid Id,
    string Solicitante,
    string PermisoSolicitado,
    string Estado,
    string? Aprobador,
    DateTime FechaCreacion,
    DateTime? FechaResolucion,
    string? MotivoRechazo);
