using AccessControl.Domain.Interfaces;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Sugerencias;
using Reports.Application.DTOs;
using Reports.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="IFuenteMetricas"/> de Reportes: junta los conteos de Control de Acceso (cuentas), HabitTracker (actividad y
/// planes) y las quejas nuevas. Es composición del Host (RD-03): Reportes no conoce a ningún módulo, y cada uno entrega solo
/// números (nunca identificadores ni datos de salud).
/// </summary>
public sealed class FuenteMetricasAdapter(
    IUsuarioRepository usuarios,
    IMetricasHabitosRepository habitos,
    ISugerenciaRepository sugerencias) : IFuenteMetricas
{
    public const int MaximoPlanes = 5;

    public async Task<ConteosProducto> ObtenerAsync(DateTime ahoraUtc, CancellationToken cancellationToken = default)
    {
        // Una a una: cada repositorio usa su propio contexto y no se comparten entre hilos.
        var cuentasNuevas = await usuarios.ContarCreadasDesdeAsync(ahoraUtc.AddDays(-7));
        var bajas = await usuarios.ContarConBajaProgramadaAsync();
        var nuevas = await sugerencias.ContarNuevasAsync(cancellationToken);

        var hasta = ahoraUtc.AddTicks(1); // incluye el instante actual
        var activas = await habitos.ContarPersonasConRegistroAsync(ahoraUtc.AddDays(-30), hasta, cancellationToken);
        var (cohorte, retenidas) = await habitos.ContarRetencionAsync(
            ahoraUtc.AddDays(-60), ahoraUtc.AddDays(-30), ahoraUtc.AddDays(-30), hasta, cancellationToken);
        var porCorreo = await habitos.ContarRegistrosPorCorreoDesdeAsync(ahoraUtc.AddDays(-7), cancellationToken);
        var planes = await habitos.PlanesMasIniciadosAsync(MaximoPlanes, cancellationToken);

        return new ConteosProducto(
            cuentasNuevas, bajas, nuevas, activas, cohorte, retenidas, porCorreo,
            [.. planes.Select(p => new PlanIniciadoDto(p.Slug, p.Cantidad))]);
    }
}
