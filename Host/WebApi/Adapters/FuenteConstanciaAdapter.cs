using HabitTracker.Application.Services;
using Reports.Application.Interfaces;
using FilaDeHabitTracker = HabitTracker.Application.Services.FilaConstanciaDto;
using FilaDeReportes = Reports.Application.DTOs.FilaConstanciaDto;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="IFuenteConstancia"/> de Reportes hacia el módulo de negocio (RF-NEG-08). Código de composición del Host:
/// Reportes no conoce a HabitTracker (RD-03); la constancia la calcula HabitTracker con sus propios datos.
/// </summary>
public sealed class FuenteConstanciaAdapter(IReporteConstanciaService constancia) : IFuenteConstancia
{
    public async Task<IReadOnlyList<FilaDeReportes>> PersonalAsync(
        Guid usuarioId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default) =>
        Mapear(await constancia.PersonalAsync(usuarioId, desde, hasta, cancellationToken));

    public async Task<IReadOnlyList<FilaDeReportes>> GlobalAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default) =>
        Mapear(await constancia.GlobalAsync(desde, hasta, cancellationToken));

    private static IReadOnlyList<FilaDeReportes> Mapear(IReadOnlyList<FilaDeHabitTracker> filas) =>
        [.. filas.Select(f => new FilaDeReportes(f.Plan, f.Personas, f.DiasConAvance, f.DiasSoloMinimo, f.DiasEnRango, f.PorcentajeConstancia))];
}
