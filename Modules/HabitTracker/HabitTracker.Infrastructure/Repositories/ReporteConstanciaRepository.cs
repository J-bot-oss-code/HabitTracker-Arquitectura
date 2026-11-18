using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class ReporteConstanciaRepository(HabitTrackerDbContext contexto) : IReporteConstanciaRepository
{
    public async Task<IReadOnlyList<PlanParaReporte>> ListarPlanesAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default)
    {
        var planes = await contexto.PlanesUsuario
            .AsNoTracking()
            .Where(p => p.Estado != EstadoPlanUsuario.Cancelado && p.FechaInicio < hastaUtcExclusivo && p.FechaFinPrevista >= desdeUtc)
            .ToListAsync(cancellationToken); // las pausas vienen con el plan
        if (planes.Count == 0)
        {
            return [];
        }

        // Un día de margen: el día local de una persona puede caer un día antes o después que el día UTC.
        var desde = desdeUtc.AddDays(-1);
        var hasta = hastaUtcExclusivo.AddDays(1);
        var ids = planes.Select(p => p.Id).ToList();
        // Un JOIN (no una subconsulta por hábito) para traer solo las columnas que el reporte usa.
        var avances = await (
            from r in contexto.RegistrosDiarios.AsNoTracking()
            join h in contexto.Habitos.AsNoTracking() on r.HabitoId equals h.Id
            where h.PlanUsuarioId != null && ids.Contains(h.PlanUsuarioId.Value) && r.Fecha >= desde && r.Fecha < hasta
            select new { PlanId = h.PlanUsuarioId!.Value, r.Fecha, r.EsVersionMinima })
            .ToListAsync(cancellationToken);

        var porPlan = avances.ToLookup(a => a.PlanId);
        return [.. planes.Select(p => new PlanParaReporte(
            p.Id, p.UsuarioId, p.Nombre, p.FechaInicio, p.FechaFinPrevista,
            [.. p.Pausas.Select(x => new PausaParaReporte(x.InicioUtc, x.FinUtc))],
            [.. porPlan[p.Id].Select(a => new AvanceParaReporte(a.Fecha, a.EsVersionMinima))]))];
    }
}
