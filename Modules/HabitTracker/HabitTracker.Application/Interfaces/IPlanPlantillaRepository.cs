using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Interfaces;

/// <summary>Lectura del catálogo de planes (Fase 3). La escritura es solo del sembrador de Infrastructure.</summary>
public interface IPlanPlantillaRepository
{
    /// <summary>Planes activos con sus tareas, sin seguimiento de cambios.</summary>
    Task<IReadOnlyList<PlanPlantilla>> ListarActivosAsync(CancellationToken cancellationToken = default);

    /// <summary>Plan activo con ese slug, o null.</summary>
    Task<PlanPlantilla?> ObtenerActivoPorSlugAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Las plantillas con esos slugs, activas o retiradas: un plan en curso conserva su guía semanal aunque la plantilla
    /// salga del catálogo. Una sola consulta para toda la lista de planes de una persona.
    /// </summary>
    Task<IReadOnlyList<PlanPlantilla>> ObtenerPorSlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default);
}
