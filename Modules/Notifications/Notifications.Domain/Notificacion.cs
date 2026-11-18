namespace Notifications.Domain;

/// <summary>Qué la originó. Cada tipo corresponde a un evento del sistema que avisa a alguien.</summary>
public enum TipoNotificacion
{
    /// <summary>Una persona creó una solicitud de permiso: se avisa a los Administradores (RF-NOT-01).</summary>
    SolicitudCreada = 1,

    /// <summary>Un Administrador resolvió una solicitud: se avisa a quien la hizo (RF-NOT-02).</summary>
    SolicitudResuelta = 2,

    /// <summary>La persona obtuvo un logro de constancia en HabitTracker (RF-NOT-03).</summary>
    LogroObtenido = 3,
}

/// <summary>
/// Un aviso para una persona, que ve dentro del sistema (canal interno, RF-NOT-06). Es suya: nadie más la consulta ni la marca
/// como leída (RF-NOT-04 y RF-NOT-05). El módulo no conoce a quien la origina: recibe el texto ya escrito.
/// </summary>
public sealed class Notificacion
{
    public const int LongitudMaximaTitulo = 120;
    public const int LongitudMaximaMensaje = 500;

    private Notificacion() { } // EF Core

    public Notificacion(Guid usuarioId, TipoNotificacion tipo, string titulo, string mensaje, DateTime creadaEnUtc)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("La persona destinataria es obligatoria.", nameof(usuarioId));
        if (!Enum.IsDefined(tipo))
            throw new ArgumentException("El tipo de notificación no es válido.", nameof(tipo));
        if (string.IsNullOrWhiteSpace(titulo))
            throw new ArgumentException("El título es obligatorio.", nameof(titulo));
        if (string.IsNullOrWhiteSpace(mensaje))
            throw new ArgumentException("El mensaje es obligatorio.", nameof(mensaje));
        if (creadaEnUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe estar en UTC.", nameof(creadaEnUtc));

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Tipo = tipo;
        Titulo = Acortar(titulo.Trim(), LongitudMaximaTitulo);
        Mensaje = Acortar(mensaje.Trim(), LongitudMaximaMensaje);
        CreadaEnUtc = creadaEnUtc;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public TipoNotificacion Tipo { get; private set; }
    public string Titulo { get; private set; } = string.Empty;
    public string Mensaje { get; private set; } = string.Empty;
    public DateTime CreadaEnUtc { get; private set; }

    /// <summary>Nulo mientras no se ha leído.</summary>
    public DateTime? LeidaEnUtc { get; private set; }

    public bool Leida => LeidaEnUtc is not null;

    /// <summary>
    /// La marca la persona destinataria. Marcar la de otra persona se rechaza con <see cref="UnauthorizedAccessException"/>
    /// (RF-NOT-05) y la notificación no cambia. Marcar una ya leída no la cambia (idempotente).
    /// </summary>
    public void MarcarLeida(Guid usuarioActualId, DateTime ahoraUtc)
    {
        if (usuarioActualId != UsuarioId)
            throw new UnauthorizedAccessException("Solo puedes marcar tus propias notificaciones.");
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe estar en UTC.", nameof(ahoraUtc));

        LeidaEnUtc ??= ahoraUtc;
    }

    private static string Acortar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
