using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Persistence;

public sealed class HabitTrackerUnitOfWork(HabitTrackerDbContext dbContext) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ViolacionesDeUnicidad.Es(ex))
        {
            // Application no conoce a EF: recibe un conflicto de unicidad y decide qué responder.
            throw new ConflictoDeUnicidadException(ex);
        }
    }

    public void Dispose() => dbContext.Dispose();
}