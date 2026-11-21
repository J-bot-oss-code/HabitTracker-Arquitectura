using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Entities;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class MetricasHabitosRepository(HabitTrackerDbContext contexto) : IMetricasHabitosRepository
{
    // Personas con registro en un rango: el UsuarioId vive en el hábito del registro. Solo se cuenta, nunca se devuelve.
    private IQueryable<Guid> PersonasConRegistro(DateTime desdeUtc, DateTime hastaUtcExclusivo) =>
        contexto.RegistrosDiarios.AsNoTracking()
            .Where(r => r.Fecha >= desdeUtc && r.Fecha < hastaUtcExclusivo)
            .Select(r => r.Habito.UsuarioId)
            .Distinct();

    public Task<int> ContarPersonasConRegistroAsync(DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default) =>
        PersonasConRegistro(desdeUtc, hastaUtcExclusivo).CountAsync(cancellationToken);

    public async Task<(int Cohorte, int Retenidas)> ContarRetencionAsync(
        DateTime anteriorDesdeUtc, DateTime anteriorHastaUtcExclusivo, DateTime actualDesdeUtc, DateTime actualHastaUtcExclusivo,
        CancellationToken cancellationToken = default)
    {
        var cohorte = PersonasConRegistro(anteriorDesdeUtc, anteriorHastaUtcExclusivo);
        var actuales = PersonasConRegistro(actualDesdeUtc, actualHastaUtcExclusivo);
        return (await cohorte.CountAsync(cancellationToken), await cohorte.Where(u => actuales.Contains(u)).CountAsync(cancellationToken));
    }

    public Task<int> ContarRegistrosPorCorreoDesdeAsync(DateTime desdeUtc, CancellationToken cancellationToken = default) =>
        contexto.RegistrosDiarios.CountAsync(r => r.Origen == OrigenRegistro.Correo && r.Fecha >= desdeUtc, cancellationToken);

    public async Task<IReadOnlyList<PlanIniciadoConteo>> PlanesMasIniciadosAsync(int maximo, CancellationToken cancellationToken = default)
    {
        // La agrupación, el orden y el tope los hace la base; el record se arma después con las pocas filas que quedan.
        var filas = await contexto.PlanesUsuario.AsNoTracking()
            .Where(p => p.Slug != null) // un plan propio (sin plantilla del catálogo) no entra en «más iniciados»
            .GroupBy(p => p.Slug!)
            .Select(g => new { Slug = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad).ThenBy(x => x.Slug)
            .Take(maximo)
            .ToListAsync(cancellationToken);
        return [.. filas.Select(f => new PlanIniciadoConteo(f.Slug, f.Cantidad))];
    }
}
