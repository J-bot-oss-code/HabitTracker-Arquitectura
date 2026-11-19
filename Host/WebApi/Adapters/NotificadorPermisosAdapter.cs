using Notifications.Application.Interfaces;
using Notifications.Domain;
using Permissions.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Conecta los avisos de Permisos con el motor de Notificaciones. Código de composición del Host: ninguno de los dos módulos
/// conoce al otro (RD-03). Decide a quién se avisa: a todos los Administradores activos de una solicitud nueva (RF-NOT-01) y
/// solo a quien la hizo de su resolución (RF-NOT-02).
/// </summary>
public sealed class NotificadorPermisosAdapter(INotificacionService notificaciones, IDirectorioUsuarios directorio) : INotificadorPermisos
{
    public async Task SolicitudCreadaAsync(string solicitante, string permisoSolicitado, CancellationToken cancellationToken = default)
    {
        var administradores = await directorio.ListarAdministradoresActivosAsync(cancellationToken);
        await notificaciones.NotificarAsync(
            administradores,
            TipoNotificacion.SolicitudCreada,
            "Nueva solicitud de permiso",
            $"{solicitante} pidió el permiso «{permisoSolicitado}». Revísala en Permisos.",
            cancellationToken);
    }

    public async Task SolicitudResueltaAsync(
        string solicitante, string permisoSolicitado, bool aprobada, string? motivoRechazo, CancellationToken cancellationToken = default)
    {
        var persona = await directorio.ObtenerPorCorreoAsync(solicitante, cancellationToken);
        if (persona is null)
        {
            return; // la cuenta ya no existe o no está activa: nadie a quien avisar
        }

        var (titulo, mensaje) = aprobada
            ? ("Solicitud aprobada", $"Un Administrador aprobó tu solicitud del permiso «{permisoSolicitado}».")
            : ("Solicitud rechazada",
               $"Un Administrador rechazó tu solicitud del permiso «{permisoSolicitado}». Motivo: {motivoRechazo}");

        await notificaciones.NotificarAsync([persona], TipoNotificacion.SolicitudResuelta, titulo, mensaje, cancellationToken);
    }
}
