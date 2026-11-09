using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Interfaces;

/// <summary>Lectura del catálogo de planes (Fase 3). La escritura es solo del sembrador de Infrastructure.</summary>
public interface IPlanPlantillaRepository
{
    /// <summary>Planes activos con sus tareas, sin seguimiento de cambios.</summary>
    Task<IReadOnlyList<PlanPlantilla>> ListarActivosAsync(CancellationToken cancellationToken = default);

    /// <summary>Plan activo con ese slug, o null.</summary>
    Task<PlanPlantilla?> ObtenerActivoPorSlugAsync(string slug, CancellationToken cancellationToken = default);
}
