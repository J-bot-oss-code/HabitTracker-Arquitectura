using AccessControl.Domain.Interfaces;
using HabitTracker.Application.Notificaciones;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="IEstadoCuentas"/> de HabitTracker hacia las cuentas de Control de Acceso. Es código de composición del
/// Host: Control de Acceso no conoce a HabitTracker (RD-03) y HabitTracker no lee sus tablas. (El «directorio» de
/// Notificaciones es otro puerto, <c>DirectorioUsuariosAdapter</c>.)
/// </summary>
public sealed class EstadoCuentasAdapter(IUsuarioRepository usuarios) : IEstadoCuentas
{
    public async Task<bool> CuentaActivaAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var usuario = await usuarios.GetByIdAsync(usuarioId);
        return usuario is { Activo: true, TieneBajaProgramada: false };
    }
}
