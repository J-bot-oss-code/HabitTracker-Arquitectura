namespace HabitTracker.Application.Interfaces;

public interface IUnitOfWork : IDisposable
{
    /// <summary>Guarda los cambios. Lanza <c>ConflictoDeUnicidadException</c> si violan una restricción única.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}