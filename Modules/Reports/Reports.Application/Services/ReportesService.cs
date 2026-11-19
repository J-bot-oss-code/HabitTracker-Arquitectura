using Reports.Application.DTOs;
using Reports.Application.Interfaces;

namespace Reports.Application.Services;

public sealed class ReportesService(IFuenteSolicitudes solicitudes, IFuenteConstancia constancia) : IReportesService
{
    /// <summary>Tope del rango: un reporte de más de un año no es un período y carga la base de datos.</summary>
    public const int MaximoDiasDelRango = 366;

    public const string Propio = "propio";
    public const string Todos = "todos";
    public const string PreguntaConstancia = "¿Qué tan constante fui en cada plan durante este período?";

    public async Task<ReportePermisosDto> PermisosPorEstadoAsync(ConsultaReporte consulta, CancellationToken cancellationToken = default)
    {
        var (desde, hasta) = ValidarRango(consulta);
        // El alcance lo decide el rol del token: un Estándar nunca pide los datos de otra persona (RF-REP-04).
        var solicitante = consulta.EsAdministrador ? null : consulta.Correo;
        var filas = await solicitudes.ContarPorEstadoAsync(
            desde.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), hasta.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            solicitante, cancellationToken);

        return new ReportePermisosDto(desde, hasta, Alcance(consulta), filas, filas.Sum(f => f.Cantidad));
    }

    public async Task<ReporteConstanciaDto> ConstanciaPorPlanAsync(ConsultaReporte consulta, CancellationToken cancellationToken = default)
    {
        var (desde, hasta) = ValidarRango(consulta);
        var filas = consulta.EsAdministrador
            ? await constancia.GlobalAsync(desde, hasta, cancellationToken)
            : await constancia.PersonalAsync(consulta.UsuarioId, desde, hasta, cancellationToken);

        return new ReporteConstanciaDto(desde, hasta, Alcance(consulta), PreguntaConstancia, filas);
    }

    private static string Alcance(ConsultaReporte consulta) => consulta.EsAdministrador ? Todos : Propio;

    private static (DateOnly Desde, DateOnly Hasta) ValidarRango(ConsultaReporte consulta)
    {
        ArgumentNullException.ThrowIfNull(consulta);
        if (consulta.UsuarioId == Guid.Empty)
            throw new UnauthorizedAccessException("Sesión inválida.");
        if (consulta.Desde is not { } desde || consulta.Hasta is not { } hasta)
            throw new ArgumentException("Indica el rango de fechas con «desde» y «hasta» (AAAA-MM-DD).");
        if (desde.Year < 2000 || hasta.Year < 2000)
            throw new ArgumentException("Las fechas del rango no son válidas.");
        if (hasta < desde)
            throw new ArgumentException("«hasta» no puede ser anterior a «desde».");
        if (hasta.DayNumber - desde.DayNumber + 1 > MaximoDiasDelRango)
            throw new ArgumentException($"El rango no puede pasar de {MaximoDiasDelRango} días.");

        return (desde, hasta);
    }
}
