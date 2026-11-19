using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Services;

/// <summary>
/// Una fila del reporte de constancia: un plan (de una persona) o, para el Administrador, un plan del catálogo con las personas
/// que lo siguieron. Los días son enteros para una persona y promedios (un decimal) para el conjunto.
/// </summary>
public sealed record FilaConstanciaDto(
    string Plan, int Personas, decimal DiasConAvance, decimal DiasSoloMinimo, decimal DiasEnRango, decimal? PorcentajeConstancia);

public interface IReporteConstanciaService
{
    /// <summary>
    /// «¿Qué tan constante fui en cada plan durante este período?» (RF-REP-02): una fila por plan de esa persona con días del
    /// rango en marcha. Solo lee lo de <paramref name="usuarioId"/> y mide por su día local.
    /// </summary>
    Task<IReadOnlyList<FilaConstanciaDto>> PersonalAsync(Guid usuarioId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lo mismo para todas las personas, agrupado por plan del catálogo, sin nombres ni correos. Mide por día UTC (el agregado
    /// no usa la zona de cada persona).
    /// </summary>
    Task<IReadOnlyList<FilaConstanciaDto>> GlobalAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default);
}

public sealed class ReporteConstanciaService(
    IPlanUsuarioRepository planes,
    IHabitoRepository habitos,
    IPerfilUsuarioRepository perfiles,
    IReporteConstanciaRepository global,
    TimeProvider? reloj = null) : IReporteConstanciaService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<IReadOnlyList<FilaConstanciaDto>> PersonalAsync(
        Guid usuarioId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var zona = Seguimiento.Zona(await perfiles.ObtenerAsync(usuarioId, cancellationToken));
        var hoy = CalculadoraProgreso.DiaLocal(zona, Guardas.Ahora(_reloj));
        var todos = await habitos.GetByUsuarioIdSoloLecturaAsync(usuarioId, cancellationToken);

        var filas = new List<FilaConstanciaDto>();
        foreach (var plan in (await planes.ListarDeUsuarioAsync(usuarioId, cancellationToken))
                     // shortcut: un cancelado de antes de guardar la fecha (nula) no se puede medir y queda fuera.
                     .Where(p => p.Estado != EstadoPlanUsuario.Cancelado || p.FechaCancelacion is not null)
                     .OrderBy(p => p.FechaInicio))
        {
            var avances = Seguimiento.HabitosDe(plan, todos)
                .SelectMany(h => h.Registros)
                .Select(r => new AvanceDelDia(CalculadoraProgreso.DiaLocal(zona, r.Fecha), r.EsVersionMinima));
            var pausas = plan.Pausas.Select(p => new PausaEnDias(
                CalculadoraProgreso.DiaLocal(zona, p.InicioUtc), p.FinUtc is { } fin ? CalculadoraProgreso.DiaLocal(zona, fin) : null));

            var c = CalculadoraConstancia.Calcular(
                desde, hasta, CalculadoraProgreso.DiaLocal(zona, plan.FechaInicio), CalculadoraProgreso.DiaLocal(zona, plan.FinEfectivo),
                hoy, pausas, avances);

            if (c.DiasEnRango > 0) // un plan que no estuvo en marcha en el rango no dice nada del rango
                filas.Add(new FilaConstanciaDto(plan.Nombre, 1, c.DiasConAvance, c.DiasSoloMinimo, c.DiasEnRango, c.PorcentajeConstancia));
        }

        return filas;
    }

    public async Task<IReadOnlyList<FilaConstanciaDto>> GlobalAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default)
    {
        var hoy = DateOnly.FromDateTime(Guardas.Ahora(_reloj));
        var delRango = await global.ListarPlanesAsync(
            desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), cancellationToken);

        var calculados = delRango
            .Select(p => (p.Nombre, p.UsuarioId, C: CalculadoraConstancia.Calcular(
                desde, hasta, DateOnly.FromDateTime(p.FechaInicioUtc), DateOnly.FromDateTime(p.FechaFinUtc), hoy,
                p.Pausas.Select(x => new PausaEnDias(DateOnly.FromDateTime(x.InicioUtc), x.FinUtc is { } fin ? DateOnly.FromDateTime(fin) : null)),
                p.Avances.Select(a => new AvanceDelDia(DateOnly.FromDateTime(a.FechaUtc), a.EsVersionMinima)))))
            .Where(x => x.C.DiasEnRango > 0);

        return [.. calculados
            .GroupBy(x => x.Nombre)
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new FilaConstanciaDto(
                g.Key,
                g.Select(x => x.UsuarioId).Distinct().Count(),
                Promedio(g.Select(x => x.C.DiasConAvance)),
                Promedio(g.Select(x => x.C.DiasSoloMinimo)),
                Promedio(g.Select(x => x.C.DiasEnRango)),
                // Ponderado: el total de días con avance sobre el total de días en marcha, no un promedio de porcentajes.
                Math.Round(g.Sum(x => x.C.DiasConAvance) * 100m / g.Sum(x => x.C.DiasEnRango), 1)))];
    }

    private static decimal Promedio(IEnumerable<int> valores) => Math.Round((decimal)valores.Average(), 1);
}
