using HabitTracker.Application.DTOs;

namespace HabitTracker.Application.Interfaces;

public interface IPlanDelDiaService
{
    Task<PlanDelDiaDto> ObtenerAsync(Guid usuarioId, string ciudad, CancellationToken cancellationToken = default);
}
