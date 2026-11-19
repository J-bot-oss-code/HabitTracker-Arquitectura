using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class PlanPlantillaRepository(HabitTrackerDbContext contexto) : IPlanPlantillaRepository
{
    public async Task<IReadOnlyList<PlanPlantilla>> ListarActivosAsync(CancellationToken cancellationToken = default) =>
        await contexto.PlanesPlantilla.AsNoTracking().Where(p => p.Activo).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlanPlantilla>> ObtenerPorSlugsAsync(
        IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default) =>
        slugs.Count == 0
            ? []
            : await contexto.PlanesPlantilla.AsNoTracking().Where(p => slugs.Contains(p.Slug)).ToListAsync(cancellationToken);

    public Task<PlanPlantilla?> ObtenerActivoPorSlugAsync(string slug, CancellationToken cancellationToken = default) =>
        contexto.PlanesPlantilla.AsNoTracking().FirstOrDefaultAsync(p => p.Activo && p.Slug == slug, cancellationToken);
}
