namespace Permissions.Application.DTOs;

public sealed record AprobarSolicitudCommand(Guid SolicitudId, string Aprobador);
