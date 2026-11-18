using AccessControl.Application.Interfaces;
using Notifications.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>Notificaciones y preferencia de correo en la descarga de datos y en la baja de cuenta.</summary>
public sealed class ParticipanteNotificaciones(INotificacionService servicio) : IParticipanteDatosUsuario
{
    public string Seccion => "notificaciones";

    public async Task<object> ExportarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        new
        {
            preferencias = await servicio.ObtenerPreferenciasAsync(usuarioId, cancelacion),
            notificaciones = await servicio.ListarAsync(usuarioId, soloNoLeidas: false, cancelacion),
        };

    public async Task EliminarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.EliminarDeUsuarioAsync(usuarioId, cancelacion);
}
