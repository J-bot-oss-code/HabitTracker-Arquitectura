using Notifications.Application.DTOs;
using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface INotificacionService
{
    /// <summary>
    /// Avisa a cada destinatario (una vez, aunque se repita): guarda su notificación interna y, si tiene el correo activo
    /// (RF-NOT-06 y RF-NOT-07), encola el correo. Un fallo al encolar no pierde la notificación interna ni la operación que
    /// la originó.
    /// </summary>
    Task NotificarAsync(
        IEnumerable<DestinatarioNotificacion> destinatarios, TipoNotificacion tipo, string titulo, string mensaje,
        CancellationToken cancellationToken = default);

    /// <summary>Las notificaciones de esa persona, leídas y no leídas (RF-NOT-04). Nunca las de otra.</summary>
    Task<IReadOnlyList<NotificacionDto>> ListarAsync(Guid usuarioId, bool soloNoLeidas, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca una como leída (RF-NOT-05). <see cref="Exceptions.NotificacionNoEncontradaException"/> si no existe;
    /// <see cref="UnauthorizedAccessException"/> si es de otra persona.
    /// </summary>
    Task MarcarLeidaAsync(Guid usuarioId, Guid notificacionId, CancellationToken cancellationToken = default);

    Task<PreferenciasNotificacionesDto> ObtenerPreferenciasAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<PreferenciasNotificacionesDto> GuardarPreferenciasAsync(Guid usuarioId, bool correoActivo, CancellationToken cancellationToken = default);

    /// <summary>Borra las notificaciones y la preferencia de esa persona (baja de cuenta). Idempotente.</summary>
    Task EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
