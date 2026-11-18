using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="IDirectorioUsuarios"/> de Notificaciones hacia las cuentas de Control de Acceso. Código de composición del
/// Host: Notificaciones no conoce a Control de Acceso (RD-03) ni lee sus tablas.
/// </summary>
public sealed class DirectorioUsuariosAdapter(IUsuarioRepository usuarios) : IDirectorioUsuarios
{
    public async Task<IReadOnlyList<DestinatarioNotificacion>> ListarAdministradoresActivosAsync(CancellationToken cancellationToken = default) =>
        [.. (await usuarios.ListarAdministradoresActivosAsync()).Select(u => new DestinatarioNotificacion(u.Id, u.Correo.Valor))];

    public async Task<DestinatarioNotificacion?> ObtenerPorCorreoAsync(string correo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(correo))
        {
            return null;
        }

        Email email;
        try
        {
            email = new Email(correo);
        }
        catch (ArgumentException)
        {
            return null; // un correo mal formado no es de nadie
        }

        var usuario = await usuarios.GetByCorreoAsync(email);
        return usuario is { Activo: true } ? new DestinatarioNotificacion(usuario.Id, usuario.Correo.Valor) : null;
    }
}
