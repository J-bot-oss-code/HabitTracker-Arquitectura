namespace Permissions.Application.DTOs;

public sealed record RechazarSolicitudCommand(Guid SolicitudId, string Aprobador, string MotivoRechazo);
