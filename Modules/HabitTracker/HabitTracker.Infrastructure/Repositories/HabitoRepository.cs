using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Entities;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class HabitoRepository(HabitTrackerDbContext dbContext) : IHabitoRepository
{
    public async Task<Habito?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.Habitos
            .Include(h => h.Registros)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Habito>> GetByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await dbContext.Habitos
            .Include(h => h.Registros)
            .Where(h => h.UsuarioId == usuarioId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Habito habito, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(habito);
        await dbContext.Habitos.AddAsync(habito, cancellationToken);
    }

    public async Task UpdateAsync(Habito habito, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(habito);
        dbContext.Habitos.Update(habito);
        await Task.CompletedTask;
    }
}