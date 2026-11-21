using Reports.Application.DTOs;
using Reports.Application.Interfaces;

namespace Reports.Application.Services;

/// <summary>
/// Las métricas del producto para el Administrador. Aquí vive la regla de privacidad (P6): lo que cuenta personas solo se
/// muestra con una cohorte de al menos <see cref="CohorteMinima"/>; con menos, es nulo. Las cuentas nuevas, las bajas en curso y
/// las quejas nuevas son datos de operación que el Administrador ya ve en sus pantallas y no se ocultan.
/// </summary>
public sealed class MetricasProductoService(IFuenteMetricas fuente, TimeProvider? reloj = null) : IMetricasProductoService
{
    public const int CohorteMinima = 5;

    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<MetricasProductoDto> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var ahora = _reloj.GetUtcNow().UtcDateTime;
        var c = await fuente.ObtenerAsync(ahora, cancellationToken);

        return new MetricasProductoDto(
            ahora,
            CohorteMinima,
            c.CuentasNuevas7d,
            c.EliminacionesEnCurso,
            c.SugerenciasNuevas,
            c.RegistrosPorCorreo7d,
            c.PersonasActivas30d >= CohorteMinima ? c.PersonasActivas30d : null,
            c.CohorteRetencion >= CohorteMinima
                ? (int)Math.Round(100.0 * c.PersonasRetenidas / c.CohorteRetencion, MidpointRounding.AwayFromZero)
                : null,
            [.. c.PlanesIniciados.Where(p => p.Cantidad >= CohorteMinima)]);
    }
}
