namespace HabitTracker.Application.Interfaces;

public interface IMetaRepository
{
    Task<bool> ExistsAsync(Guid metaId, CancellationToken cancellationToken = default);
}