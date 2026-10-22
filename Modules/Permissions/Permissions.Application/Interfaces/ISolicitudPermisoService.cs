using Permissions.Application.DTOs;

namespace Permissions.Application.Interfaces;

public interface ISolicitudPermisoService
{
    Task<Guid> CrearAsync(CrearSolicitudPermisoCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermisoDto>> ConsultarAsync(ObtenerSolicitudesQuery query, CancellationToken cancellationToken = default);

    Task<SolicitudPermisoDto> ObtenerPorIdAsync(Guid id, string usuarioConsulta, string rolUsuario, CancellationToken cancellationToken = default);

    Task AprobarAsync(AprobarSolicitudCommand command, CancellationToken cancellationToken = default);

    Task RechazarAsync(RechazarSolicitudCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermisoDto>> EjecutarAccionProtegidaAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default);
}
