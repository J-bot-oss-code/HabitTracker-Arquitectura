using Microsoft.EntityFrameworkCore;
using Notifications.Application.Interfaces;
using Notifications.Domain;
using Notifications.Infrastructure.Persistence;

namespace Notifications.Infrastructure.Repositories;

public sealed class NotificacionRepository(NotificationsDbContext dbContext) : INotificacionRepository
{
    public async Task AgregarAsync(Notificacion notificacion, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notificacion);
        await dbContext.Notificaciones.AddAsync(notificacion, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // Siempre por dueña: una consulta nunca devuelve la notificación de otra persona (RF-NOT-04).
    public async Task<IReadOnlyList<Notificacion>> ListarDeUsuarioAsync(Guid usuarioId, bool soloNoLeidas, CancellationToken cancellationToken = default)
    {
        var consulta = dbContext.Notificaciones.AsNoTracking().Where(n => n.UsuarioId == usuarioId);
        if (soloNoLeidas)
        {
            consulta = consulta.Where(n => n.LeidaEnUtc == null);
        }

        return await consulta.OrderByDescending(n => n.CreadaEnUtc).ToListAsync(cancellationToken);
    }

    // Con seguimiento: el servicio la marca como leída y guarda los cambios.
    public Task<Notificacion?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Notificaciones.FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public Task GuardarCambiosAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

    public Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        dbContext.Notificaciones.Where(n => n.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);
}

public sealed class PreferenciaNotificacionesRepository(NotificationsDbContext dbContext) : IPreferenciaNotificacionesRepository
{
    public Task<PreferenciaNotificaciones?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        dbContext.PreferenciasNotificaciones.FirstOrDefaultAsync(p => p.UsuarioId == usuarioId, cancellationToken);

    public async Task AgregarAsync(PreferenciaNotificaciones preferencia, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferencia);
        await dbContext.PreferenciasNotificaciones.AddAsync(preferencia, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Una petición simultánea de la misma persona la insertó primero: se queda el último valor pedido, no un 500.
            dbContext.Entry(preferencia).State = EntityState.Detached;
            var existente = await dbContext.PreferenciasNotificaciones
                .FirstOrDefaultAsync(p => p.UsuarioId == preferencia.UsuarioId, cancellationToken);
            if (existente is null)
            {
                throw; // el fallo era otro: no se esconde
            }

            existente.CambiarCorreo(preferencia.CorreoActivo);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task GuardarCambiosAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);

    public async Task EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await dbContext.PreferenciasNotificaciones.Where(p => p.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);
}
