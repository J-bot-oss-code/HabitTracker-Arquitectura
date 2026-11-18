using Notifications.Domain;

namespace Notifications.Application.DTOs;

/// <summary>Lo que ve la persona dueña de la notificación.</summary>
public sealed record NotificacionDto(
    Guid Id,
    TipoNotificacion Tipo,
    string Titulo,
    string Mensaje,
    DateTime CreadaEnUtc,
    bool Leida,
    DateTime? LeidaEnUtc);

/// <summary>Cómo quiere recibir sus avisos una persona (RF-NOT-07).</summary>
public sealed record PreferenciasNotificacionesDto(bool CorreoActivo);

/// <summary>Una persona a la que se avisa: su identificador (la notificación interna) y su correo (el canal de correo).</summary>
public sealed record DestinatarioNotificacion(Guid UsuarioId, string Correo);
