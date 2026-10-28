using HabitTracker.Domain.Entities;

namespace HabitTracker.Application.Interfaces;

public interface IHabitoRepository
{
    Task<Habito?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Habito>> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task AddAsync(Habito habito, CancellationToken cancellationToken = default);

    Task UpdateAsync(Habito habito, CancellationToken cancellationToken = default);
}