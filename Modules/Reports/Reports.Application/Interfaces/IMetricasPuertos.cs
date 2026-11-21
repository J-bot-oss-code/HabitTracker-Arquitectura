using Reports.Application.DTOs;

namespace Reports.Application.Interfaces;

/// <summary>Conteos crudos del producto, tal como los entregan Control de Acceso y HabitTracker. Solo números, sin identificadores.</summary>
public sealed record ConteosProducto(
    int CuentasNuevas7d,
    int EliminacionesEnCurso,
    int SugerenciasNuevas,
    int PersonasActivas30d,
    int CohorteRetencion,
    int PersonasRetenidas,
    int RegistrosPorCorreo7d,
    IReadOnlyList<PlanIniciadoDto> PlanesIniciados);

/// <summary>Los conteos de las métricas del Administrador. Lo implementa un adaptador del Host sobre otros módulos (RD-03).</summary>
public interface IFuenteMetricas
{
    Task<ConteosProducto> ObtenerAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default);
}

public interface IMetricasProductoService
{
    /// <summary>
    /// Las métricas agregadas y anónimas del producto (Fase 6, solo Administrador). Todo lo que cuenta personas se oculta (nulo)
    /// cuando la cohorte es menor de la mínima: con pocas personas una cifra deja de ser anónima.
    /// </summary>
    Task<MetricasProductoDto> ObtenerAsync(CancellationToken cancellationToken = default);
}
