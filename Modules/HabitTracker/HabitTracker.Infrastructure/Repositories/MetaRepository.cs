using HabitTracker.Application.Interfaces;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class MetaRepository(HabitTrackerDbContext dbContext) : IMetaRepository
{
    public Task<bool> ExistsAsync(Guid metaId, CancellationToken cancellationToken = default) =>
        dbContext.Metas.AnyAsync(m => m.Id == metaId, cancellationToken);
}