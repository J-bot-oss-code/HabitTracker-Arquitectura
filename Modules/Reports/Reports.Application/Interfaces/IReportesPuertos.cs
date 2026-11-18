using Reports.Application.DTOs;

namespace Reports.Application.Interfaces;

/// <summary>
/// Las solicitudes de permiso del Core, ya agregadas por estado. Lo implementa un adaptador del Host sobre Permisos (RD-03):
/// la agrupación la hace la base de datos, no este módulo.
/// </summary>
public interface IFuenteSolicitudes
{
    /// <summary>Creadas desde <paramref name="desdeUtc"/> (incluida) hasta <paramref name="hastaUtcExclusivo"/> (excluida). Con solicitante, solo las de esa persona.</summary>
    Task<IReadOnlyList<FilaPermisosDto>> ContarPorEstadoAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, string? solicitante, CancellationToken cancellationToken = default);
}

/// <summary>La constancia en los planes del negocio, ya calculada. Lo implementa un adaptador del Host sobre HabitTracker (RD-03).</summary>
public interface IFuenteConstancia
{
    /// <summary>Los planes de esa persona, por su día local.</summary>
    Task<IReadOnlyList<FilaConstanciaDto>> PersonalAsync(Guid usuarioId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default);

    /// <summary>Todos los planes agrupados por plan del catálogo, sin identificar a nadie.</summary>
    Task<IReadOnlyList<FilaConstanciaDto>> GlobalAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken = default);
}

public interface IReportesService
{
    /// <summary>
    /// Solicitudes de permiso por estado y período (RF-REP-01). Un Estándar solo cuenta las suyas; un Administrador, las de todas
    /// (RF-REP-04). <see cref="ArgumentException"/> si el rango falta o no es válido.
    /// </summary>
    Task<ReportePermisosDto> PermisosPorEstadoAsync(ConsultaReporte consulta, CancellationToken cancellationToken = default);

    /// <summary>
    /// La constancia por plan en el período (RF-REP-02). Un Estándar ve sus planes; un Administrador, el agregado por plan del
    /// catálogo sin nombres ni correos (RF-REP-04).
    /// </summary>
    Task<ReporteConstanciaDto> ConstanciaPorPlanAsync(ConsultaReporte consulta, CancellationToken cancellationToken = default);
}
