namespace Notifications.Application.Exceptions;

/// <summary>La notificación no existe. Se traduce a 404 con el código <c>notificacion_no_encontrada</c> (RD-08).</summary>
public sealed class NotificacionNoEncontradaException() : InvalidOperationException("La notificación no existe.")
{
    public const string Codigo = "notificacion_no_encontrada";
}
