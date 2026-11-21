namespace HabitTracker.Application.Interfaces;

/// <summary>Cuántas personas distintas, cuántos registros y cuántos planes iniciados por cada plan del catálogo (solo slug y número).</summary>
public sealed record PlanIniciadoConteo(string Slug, int Cantidad);

/// <summary>
/// Conteos del producto para las métricas del Administrador (Fase 6). Solo números: nunca devuelve identificadores de personas
/// ni toca las tablas de salud (perfil, cuestionario, peso o medidas). La agrupación la hace la base de datos.
/// </summary>
public interface IMetricasHabitosRepository
{
    /// <summary>Personas distintas con al menos un registro de avance con fecha en [desde, hasta).</summary>
    Task<int> ContarPersonasConRegistroAsync(DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retención: de las personas con registro en [anteriorDesde, anteriorHasta) (la cohorte), cuántas también registraron algo en
    /// [actualDesde, actualHasta).
    /// </summary>
    Task<(int Cohorte, int Retenidas)> ContarRetencionAsync(
        DateTime anteriorDesdeUtc, DateTime anteriorHastaUtcExclusivo, DateTime actualDesdeUtc, DateTime actualHastaUtcExclusivo,
        CancellationToken cancellationToken = default);

    /// <summary>Registros hechos con «Ya lo hice» del correo con fecha desde esa UTC (incluida).</summary>
    Task<int> ContarRegistrosPorCorreoDesdeAsync(DateTime desdeUtc, CancellationToken cancellationToken = default);

    /// <summary>Planes iniciados por plan del catálogo, de más a menos, hasta <paramref name="maximo"/>.</summary>
    Task<IReadOnlyList<PlanIniciadoConteo>> PlanesMasIniciadosAsync(int maximo, CancellationToken cancellationToken = default);
}
