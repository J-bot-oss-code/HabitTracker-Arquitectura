using AccessControl.Application.Interfaces;
using AccessControl.Domain.Interfaces;
using HabitTracker.Application.Notificaciones;
using Notifications.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="ICanalRecordatorios"/> de HabitTracker: la cuenta (Control de Acceso), la preferencia de correo
/// (Notificaciones) y la cola de correos (Control de Acceso), todo por sus interfaces de Application (RD-03). Es composición del
/// Host: ningún módulo conoce a los otros.
/// </summary>
public sealed class CanalRecordatoriosAdapter(
    IUsuarioRepository usuarios,
    INotificacionService notificaciones,
    IEmailQueue cola) : ICanalRecordatorios
{
    public async Task<string?> DestinatarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.GetByIdAsync(usuarioId);
        if (usuario is not { Activo: true, TieneBajaProgramada: false })
            return null;

        // El interruptor general de correos de Notificaciones también apaga los recordatorios.
        var preferencias = await notificaciones.ObtenerPreferenciasAsync(usuarioId, cancellationToken);
        return preferencias.CorreoActivo ? usuario.Correo.Valor : null;
    }

    public Task EncolarAsync(string destinatario, CorreoRecordatorio correo, CancellationToken cancellationToken = default) =>
        cola.EncolarCorreoConAccionesAsync(
            destinatario, correo.Asunto, correo.Titulo, correo.Mensaje,
            [.. correo.Acciones.Select(a => new AccionCorreo(a.Texto, a.Url))],
            correo.EnlaceBaja);
}
