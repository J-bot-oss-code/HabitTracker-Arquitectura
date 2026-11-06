using Permissions.Application.DTOs;

namespace Permissions.Application.Interfaces;

public interface ISolicitudPermisoService
{
    Task<Guid> CrearAsync(CrearSolicitudPermisoCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermisoDto>> ConsultarAsync(ObtenerSolicitudesQuery query, CancellationToken cancellationToken = default);

    Task<SolicitudPermisoDto> ObtenerPorIdAsync(Guid id, string usuarioConsulta, string rolUsuario, CancellationToken cancellationToken = default);

    Task AprobarAsync(AprobarSolicitudCommand command, CancellationToken cancellationToken = default);

    Task RechazarAsync(RechazarSolicitudCommand command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra definitivamente las solicitudes que esa persona hizo (baja de cuenta). Las que resolvió como aprobador no se
    /// tocan: son decisiones de administración sobre otras personas.
    /// </summary>
    Task<int> EliminarDeSolicitanteAsync(string solicitante, CancellationToken cancellationToken = default);

    Task<bool> TienePermisoEfectivoAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SolicitudPermisoDto>> EjecutarAccionProtegidaAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default);
}
