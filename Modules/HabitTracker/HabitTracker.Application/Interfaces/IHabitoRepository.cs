using HabitTracker.Domain.Entities;

namespace HabitTracker.Application.Interfaces;

public interface IHabitoRepository
{
    Task<Habito?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Habito>> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task AddAsync(Habito habito, CancellationToken cancellationToken = default);

    Task UpdateAsync(Habito habito, CancellationToken cancellationToken = default);

    /// <summary>
    /// Borra definitivamente todos los hábitos del usuario con sus registros de avance y sus etiquetas asignadas (baja de
    /// cuenta). Se ejecuta de inmediato en la base de datos y es idempotente. Devuelve cuántos hábitos borró.
    /// </summary>
    Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}