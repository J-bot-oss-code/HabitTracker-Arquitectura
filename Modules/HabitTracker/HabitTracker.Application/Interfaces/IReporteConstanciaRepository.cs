namespace HabitTracker.Application.Interfaces;

public sealed record PausaParaReporte(DateTime InicioUtc, DateTime? FinUtc);

public sealed record AvanceParaReporte(DateTime FechaUtc, bool EsVersionMinima);

/// <summary>
/// Un plan reducido a lo que el reporte de constancia necesita: sus fechas, sus pausas y sus avances. No lleva el nombre ni el
/// correo de la persona, ni peso ni medidas: el reporte de todas las personas no puede identificar a nadie.
/// </summary>
public sealed record PlanParaReporte(
    Guid PlanId, Guid UsuarioId, string Nombre, DateTime FechaInicioUtc, DateTime FechaFinUtc,
    IReadOnlyList<PausaParaReporte> Pausas, IReadOnlyList<AvanceParaReporte> Avances);

/// <summary>Los planes de todas las personas para el reporte del Administrador (RF-REP-02 y RF-REP-04).</summary>
public interface IReporteConstanciaRepository
{
    /// <summary>
    /// Los planes que no se cancelaron y estuvieron en marcha en algún momento entre <paramref name="desdeUtc"/> (incluida) y
    /// <paramref name="hastaUtcExclusivo"/> (excluida), con las pausas y los avances de ese tramo (más un día de margen a cada lado
    /// para el cambio de día local). Los cancelados se dejan fuera: no guardan la fecha en que se dejaron.
    /// </summary>
    Task<IReadOnlyList<PlanParaReporte>> ListarPlanesAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken = default);
}
