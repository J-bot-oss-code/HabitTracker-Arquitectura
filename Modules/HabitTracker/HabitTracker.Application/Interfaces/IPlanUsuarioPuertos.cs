using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Motivacion;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Interfaces;

/// <summary>Los planes que una persona sigue (Fase 4). Siempre acotados a su dueña: nunca se lee un plan por id a secas.</summary>
public interface IPlanUsuarioRepository
{
    /// <summary>Un plan de esa persona con sus pausas, o null si no existe o es de otra (no se distingue: RD-06).</summary>
    Task<PlanUsuario?> ObtenerAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Todos sus planes con sus pausas, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<PlanUsuario>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task AgregarAsync(PlanUsuario plan, CancellationToken cancellationToken = default);

    /// <summary>Borra los planes y sus pausas (baja de cuenta). Inmediato e idempotente.</summary>
    Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public interface ILogroUsuarioRepository
{
    Task<IReadOnlyList<LogroUsuario>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda el logro si la persona todavía no lo tiene, de inmediato y sin pasar por el seguimiento de cambios. Es
    /// idempotente y a prueba de dos peticiones simultáneas: si otra lo guardó antes, no hace nada. Devuelve si lo guardó.
    /// </summary>
    Task<bool> AgregarSiNoExisteAsync(LogroUsuario logro, CancellationToken cancellationToken = default);

    Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

/// <summary>Los mensajes de motivación (<c>mensajes-motivacion.json</c>, embebido). El contenido es de Infrastructure.</summary>
public interface ICatalogoMensajes
{
    IReadOnlyList<MensajeMotivacion> Todos { get; }
}
