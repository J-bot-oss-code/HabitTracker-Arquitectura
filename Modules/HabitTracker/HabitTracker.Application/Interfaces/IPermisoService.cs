namespace HabitTracker.Application.Interfaces;

public interface IPermisoService
{
    Task<bool> TienePermisoEfectivoAsync(
        string solicitante,
        string permisoSolicitado,
        CancellationToken cancellationToken = default);
}