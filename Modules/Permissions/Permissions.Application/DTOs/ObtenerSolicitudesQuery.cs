namespace Permissions.Application.DTOs;

public sealed record ObtenerSolicitudesQuery(string UsuarioConsulta, string RolUsuario, bool SoloPendientes = true);
