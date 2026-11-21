using HabitTracker.Application.Sugerencias;
using HabitTracker.Domain.Sugerencias;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class SugerenciaRepository(HabitTrackerDbContext contexto) : ISugerenciaRepository
{
    public async Task AgregarAsync(Sugerencia sugerencia, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sugerencia);
        await contexto.Sugerencias.AddAsync(sugerencia, cancellationToken);
    }

    public Task<Sugerencia?> ObtenerAsync(Guid id, CancellationToken cancellationToken = default) =>
        contexto.Sugerencias.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Sugerencia>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await contexto.Sugerencias.AsNoTracking()
            .Where(s => s.UsuarioId == usuarioId)
            .OrderByDescending(s => s.CreadaEnUtc).ThenByDescending(s => s.Id)
            .ToListAsync(cancellationToken);

    public Task<int> ContarDesdeAsync(Guid usuarioId, DateTime desdeUtc, CancellationToken cancellationToken = default) =>
        contexto.Sugerencias.CountAsync(s => s.UsuarioId == usuarioId && s.CreadaEnUtc >= desdeUtc, cancellationToken);

    public async Task<(int Total, IReadOnlyList<Sugerencia> Pagina)> ListarAsync(
        EstadoSugerencia? estado, int pagina, int tamano, CancellationToken cancellationToken = default)
    {
        // Filtrado, conteo y paginación en la base: la bandeja puede crecer.
        var consulta = contexto.Sugerencias.AsNoTracking().AsQueryable();
        if (estado is { } e)
            consulta = consulta.Where(s => s.Estado == e);

        var total = await consulta.CountAsync(cancellationToken);
        var lista = await consulta
            .OrderByDescending(s => s.CreadaEnUtc).ThenByDescending(s => s.Id) // el desempate evita repetir o saltar filas entre páginas
            .Skip((pagina - 1) * tamano).Take(tamano)
            .ToListAsync(cancellationToken);
        return (total, lista);
    }

    public Task<int> ContarNuevasAsync(CancellationToken cancellationToken = default) =>
        contexto.Sugerencias.CountAsync(s => s.Estado == EstadoSugerencia.Nueva, cancellationToken);

    public async Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await contexto.Sugerencias.Where(s => s.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);
}
