using Microsoft.Extensions.Logging;
using Notifications.Application.DTOs;
using Notifications.Application.Exceptions;
using Notifications.Application.Interfaces;
using Notifications.Domain;

namespace Notifications.Application.Services;

public sealed class NotificacionService(
    INotificacionRepository notificaciones,
    IPreferenciaNotificacionesRepository preferencias,
    IColaCorreos cola,
    ILogger<NotificacionService> logger,
    TimeProvider? reloj = null) : INotificacionService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task NotificarAsync(
        IEnumerable<DestinatarioNotificacion> destinatarios, TipoNotificacion tipo, string titulo, string mensaje,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destinatarios);
        var ahora = _reloj.GetUtcNow().UtcDateTime;

        // Una notificación por persona, aunque el mismo destinatario venga repetido.
        foreach (var destinatario in destinatarios.DistinctBy(d => d.UsuarioId))
        {
            // Primero el canal interno (siempre): es lo que no se puede perder (RF-NOT-06).
            await notificaciones.AgregarAsync(new Notificacion(destinatario.UsuarioId, tipo, titulo, mensaje, ahora), cancellationToken);

            var correoActivo = (await preferencias.ObtenerAsync(destinatario.UsuarioId, cancellationToken))?.CorreoActivo ?? true;
            if (!correoActivo || string.IsNullOrWhiteSpace(destinatario.Correo))
            {
                continue; // RF-NOT-07: sin correo activo, la interna queda y no se encola nada.
            }

            try
            {
                await cola.EncolarAsync(destinatario.Correo, titulo, titulo, mensaje, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // La notificación interna ya está guardada; no se pierde ni se deshace la operación que la originó.
                logger.LogWarning(ex, "No se pudo encolar el correo de la notificación {Tipo}.", tipo);
            }
        }
    }

    public async Task<IReadOnlyList<NotificacionDto>> ListarAsync(Guid usuarioId, bool soloNoLeidas, CancellationToken cancellationToken = default)
    {
        ExigirIdentidad(usuarioId);
        var lista = await notificaciones.ListarDeUsuarioAsync(usuarioId, soloNoLeidas, cancellationToken);
        return [.. lista.Select(n => new NotificacionDto(n.Id, n.Tipo, n.Titulo, n.Mensaje, n.CreadaEnUtc, n.Leida, n.LeidaEnUtc))];
    }

    public async Task MarcarLeidaAsync(Guid usuarioId, Guid notificacionId, CancellationToken cancellationToken = default)
    {
        ExigirIdentidad(usuarioId);
        var notificacion = await notificaciones.ObtenerAsync(notificacionId, cancellationToken)
            ?? throw new NotificacionNoEncontradaException();

        notificacion.MarcarLeida(usuarioId, _reloj.GetUtcNow().UtcDateTime); // la de otra persona lanza UnauthorizedAccessException (403)
        await notificaciones.GuardarCambiosAsync(cancellationToken);
    }

    public async Task<PreferenciasNotificacionesDto> ObtenerPreferenciasAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        ExigirIdentidad(usuarioId);
        var preferencia = await preferencias.ObtenerAsync(usuarioId, cancellationToken);
        return new PreferenciasNotificacionesDto(preferencia?.CorreoActivo ?? true);
    }

    public async Task<PreferenciasNotificacionesDto> GuardarPreferenciasAsync(Guid usuarioId, bool correoActivo, CancellationToken cancellationToken = default)
    {
        ExigirIdentidad(usuarioId);
        var preferencia = await preferencias.ObtenerAsync(usuarioId, cancellationToken);
        if (preferencia is null)
        {
            await preferencias.AgregarAsync(new PreferenciaNotificaciones(usuarioId, correoActivo), cancellationToken);
        }
        else
        {
            preferencia.CambiarCorreo(correoActivo);
            await preferencias.GuardarCambiosAsync(cancellationToken);
        }

        return new PreferenciasNotificacionesDto(correoActivo);
    }

    public async Task EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        ExigirIdentidad(usuarioId);
        await notificaciones.EliminarDeUsuarioAsync(usuarioId, cancellationToken);
        await preferencias.EliminarDeUsuarioAsync(usuarioId, cancellationToken);
    }

    private static void ExigirIdentidad(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Sesión inválida.");
        }
    }
}
