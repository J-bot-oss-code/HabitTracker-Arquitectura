using HabitTracker.Domain.Entities;

namespace HabitTracker.Application.Interfaces;

public interface IHabitoRepository
{
    Task<Habito?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Habito>> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Los hábitos de un plan de esa persona, con sus registros y con seguimiento (para cambiarlos).</summary>
    Task<IReadOnlyList<Habito>> GetByPlanUsuarioIdAsync(Guid usuarioId, Guid planUsuarioId, CancellationToken cancellationToken = default);

    /// <summary>Todos los hábitos de la persona con sus registros, SIN seguimiento: para consultas que solo leen (Hoy, resumen).</summary>
    Task<IReadOnlyList<Habito>> GetByUsuarioIdSoloLecturaAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task AddAsync(Habito habito, CancellationToken cancellationToken = default);

    Task UpdateAsync(Habito habito, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra definitivamente todos los hábitos del usuario con sus registros de avance y sus etiquetas asignadas (baja de
    /// cuenta). Se ejecuta de inmediato en la base de datos y es idempotente. Devuelve cuántos hábitos borró.
    /// </summary>
    Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}