namespace Notifications.Domain;

/// <summary>
/// Cómo quiere recibir sus avisos una persona. El canal interno siempre está activo; el correo se puede desactivar sin perder
/// ninguna notificación interna (RF-NOT-07). Sin registro, el correo está activo.
/// </summary>
public sealed class PreferenciaNotificaciones
{
    private PreferenciaNotificaciones() { } // EF Core

    public PreferenciaNotificaciones(Guid usuarioId, bool correoActivo = true)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("La persona es obligatoria.", nameof(usuarioId));

        UsuarioId = usuarioId;
        CorreoActivo = correoActivo;
    }

    public Guid UsuarioId { get; private set; }
    public bool CorreoActivo { get; private set; } = true;

    public void CambiarCorreo(bool activo) => CorreoActivo = activo;
}
