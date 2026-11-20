namespace Permissions.Application.Interfaces;

/// <summary>
/// Punto de extensión: deja constancia en el historial de auditoría de quién resolvió una solicitud de permiso y cómo (RF-AUD-02).
/// Permisos no sabe dónde se guarda: lo implementa un adaptador del Host (RD-03). Es opcional: sin él, el módulo funciona igual.
/// A diferencia del aviso al solicitante, un fallo al registrar NO se traga: la resolución responde error, para que no quede
/// una solicitud resuelta sin su registro.
/// </summary>
public interface IAuditorPermisos
{
    /// <summary>
    /// Una solicitud pasó de <paramref name="valorAnterior"/> a <paramref name="valorNuevo"/> por la resolución del
    /// Administrador <paramref name="aprobador"/> (su correo, tal como viene del token).
    /// </summary>
    Task ResolucionRegistradaAsync(
        string aprobador, Guid solicitudId, string accion, string valorAnterior, string valorNuevo, CancellationToken cancellationToken = default);
}
