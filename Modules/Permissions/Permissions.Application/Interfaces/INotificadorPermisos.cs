namespace Permissions.Application.Interfaces;

/// <summary>
/// Punto de extensión: avisa de lo que pasa con una solicitud de permiso (RF-NOT-01 y RF-NOT-02). Permisos no sabe quién recibe
/// el aviso ni por qué canal: lo implementa un adaptador del Host (RD-03). Es opcional: sin él, el módulo funciona igual.
/// </summary>
public interface INotificadorPermisos
{
    /// <summary>Una persona creó una solicitud: se avisa a los Administradores.</summary>
    Task SolicitudCreadaAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default);

    /// <summary>Un Administrador resolvió la solicitud: se avisa a quien la hizo, con el motivo si fue rechazada.</summary>
    Task SolicitudResueltaAsync(
        string solicitante, string permisoSolicitado, bool aprobada, string? motivoRechazo, CancellationToken cancellationToken = default);
}
