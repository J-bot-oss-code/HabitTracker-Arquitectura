using AccessControl.Application.Interfaces;
using Notifications.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="IColaCorreos"/> de Notificaciones hacia la cola de correos de Control de Acceso, que ya tiene el proceso
/// que los envía (RF-NOT-08 y RF-NOT-09). Solo encola: no envía nada en la operación que origina la notificación.
/// </summary>
public sealed class ColaCorreosAdapter(IEmailQueue cola) : IColaCorreos
{
    public Task EncolarAsync(string destinatario, string asunto, string titulo, string mensaje, CancellationToken cancellationToken = default) =>
        cola.EncolarCorreoAsync(destinatario, asunto, titulo, mensaje);
}
