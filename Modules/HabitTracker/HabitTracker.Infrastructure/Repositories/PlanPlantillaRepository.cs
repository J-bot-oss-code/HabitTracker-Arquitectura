using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class PlanPlantillaRepository(HabitTrackerDbContext contexto) : IPlanPlantillaRepository
{
    public async Task<IReadOnlyList<PlanPlantilla>> ListarActivosAsync(CancellationToken cancellationToken = default) =>
        await contexto.PlanesPlantilla.AsNoTracking().Where(p => p.Activo).ToListAsync(cancellationToken);

    public Task<PlanPlantilla?> ObtenerActivoPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
        contexto.PlanesPlantilla.AsNoTracking().FirstOrDefaultAsync(p => p.Activo && p.Slug == slug, cancellationToken);
}
