using Notifications.Application.DTOs;
using Notifications.Domain;

namespace Notifications.Application.Interfaces;

public interface INotificacionRepository
{
    Task AgregarAsync(Notificacion notificacion, CancellationToken cancellationToken = default);

    /// <summary>Las de esa persona, de la más reciente a la más antigua. Nunca las de otra.</summary>
    Task<IReadOnlyList<Notificacion>> ListarDeUsuarioAsync(Guid usuarioId, bool soloNoLeidas, CancellationToken cancellationToken = default);

    /// <summary>Por id, de cualquier persona: el servicio decide si quien pregunta es su dueña.</summary>
    Task<Notificacion?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default);

    Task GuardarCambiosAsync(CancellationToken cancellationToken = default);

    /// <summary>Borra todas las de esa persona (baja de cuenta). Idempotente.</summary>
    Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface IPreferenciaNotificacionesRepository
{
    Task<PreferenciaNotificaciones?> ObtenerAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task AgregarAsync(PreferenciaNotificaciones preferencia, CancellationToken cancellationToken = default);

    Task GuardarCambiosAsync(CancellationToken cancellationToken = default);

    Task EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

/// <summary>
/// La cola de correos, vista desde Notificaciones. Solo encola: el envío lo hace un proceso aparte (RF-NOT-08). Lo implementa
/// un adaptador del Host sobre la cola de Control de Acceso (RD-03).
/// </summary>
public interface IColaCorreos
{
    Task EncolarAsync(string destinatario, string asunto, string titulo, string mensaje, CancellationToken cancellationToken = default);
}

/// <summary>Las personas del sistema, vistas desde Notificaciones. Lo implementa un adaptador del Host sobre Control de Acceso.</summary>
public interface IDirectorioUsuarios
{
    /// <summary>Todos los Administradores con la cuenta activa.</summary>
    Task<IReadOnlyList<DestinatarioNotificacion>> ListarAdministradoresActivosAsync(CancellationToken cancellationToken = default);

    /// <summary>La persona con ese correo, o <c>null</c> si no existe o su cuenta no está activa.</summary>
    Task<DestinatarioNotificacion?> ObtenerPorCorreoAsync(string correo, CancellationToken cancellationToken = default);
}
