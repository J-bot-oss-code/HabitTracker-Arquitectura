using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class AdjuntoPlanRepository(HabitTrackerDbContext contexto) : IAdjuntoPlanRepository
{
    // Siempre por dueña: el vínculo de otra persona ni se encuentra (RD-06).
    public async Task<IReadOnlyList<AdjuntoPlan>> ListarDePlanAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default) =>
        await contexto.AdjuntosPlan
            .Where(a => a.PlanUsuarioId == planId && a.UsuarioId == usuarioId)
            .OrderBy(a => a.AdjuntadoEnUtc)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AdjuntoConPlan>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        var filas = await contexto.AdjuntosPlan
            .Where(a => a.UsuarioId == usuarioId)
            .Join(contexto.PlanesUsuario.Where(p => p.UsuarioId == usuarioId), a => a.PlanUsuarioId, p => p.Id,
                (a, p) => new { Adjunto = a, p.Nombre, p.Estado })
            .OrderBy(x => x.Adjunto.AdjuntadoEnUtc)
            .ToListAsync(cancellationToken);
        return [.. filas.Select(x => new AdjuntoConPlan(x.Adjunto, x.Nombre, x.Estado))];
    }

    public Task<AdjuntoPlan?> ObtenerAsync(Guid planId, Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default) =>
        contexto.AdjuntosPlan.FirstOrDefaultAsync(
            a => a.PlanUsuarioId == planId && a.DocumentoId == documentoId && a.UsuarioId == usuarioId, cancellationToken);

    public async Task AgregarAsync(AdjuntoPlan adjunto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adjunto);
        await contexto.AdjuntosPlan.AddAsync(adjunto, cancellationToken);
    }

    public Task QuitarAsync(AdjuntoPlan adjunto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adjunto);
        contexto.AdjuntosPlan.Remove(adjunto);
        return Task.CompletedTask;
    }

    public Task<bool> DocumentoEnPlanEnCursoAsync(Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default) =>
        contexto.AdjuntosPlan
            .Where(a => a.DocumentoId == documentoId && a.UsuarioId == usuarioId)
            .Join(contexto.PlanesUsuario, a => a.PlanUsuarioId, p => p.Id, (a, p) => p.Estado)
            .AnyAsync(e => e == EstadoPlanUsuario.Activo || e == EstadoPlanUsuario.Pausado, cancellationToken);
}
