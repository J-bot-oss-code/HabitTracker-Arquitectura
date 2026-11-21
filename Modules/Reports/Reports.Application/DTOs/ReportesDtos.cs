namespace Reports.Application.DTOs;

/// <summary>Quién consulta y qué rango pide. La identidad y el rol salen del token, nunca del cuerpo (RD-06).</summary>
public sealed record ConsultaReporte(Guid UsuarioId, string Correo, bool EsAdministrador, DateOnly? Desde, DateOnly? Hasta);

/// <summary>Cuántas solicitudes de permiso hay en un estado.</summary>
public sealed record FilaPermisosDto(string Estado, int Cantidad);

/// <summary>Solicitudes de permiso por estado en un rango (RF-REP-01). <c>Alcance</c> es «propio» o «todos».</summary>
public sealed record ReportePermisosDto(DateOnly Desde, DateOnly Hasta, string Alcance, IReadOnlyList<FilaPermisosDto> Filas, int Total);

/// <summary>La constancia en un plan: de una persona (<c>Personas</c> = 1) o, para el Administrador, de un plan del catálogo.</summary>
public sealed record FilaConstanciaDto(
    string Plan, int Personas, decimal DiasConAvance, decimal DiasSoloMinimo, decimal DiasEnRango, decimal? PorcentajeConstancia);

/// <summary>«¿Qué tan constante fui en cada plan durante este período?» (RF-REP-02).</summary>
public sealed record ReporteConstanciaDto(DateOnly Desde, DateOnly Hasta, string Alcance, string Pregunta, IReadOnlyList<FilaConstanciaDto> Filas);

/// <summary>Un plan del catálogo y cuántas personas lo iniciaron (solo el slug y el número).</summary>
public sealed record PlanIniciadoDto(string Slug, int Cantidad);

/// <summary>
/// Métricas agregadas y anónimas del producto. Nunca llevan identificadores ni datos de salud. Lo que cuenta personas es nulo
/// cuando son menos de <paramref name="CohorteMinima"/> (el cliente puede decir por qué no se muestra).
/// </summary>
/// <param name="Retencion30d">% de quienes registraron algo hace 30 a 60 días y también en los últimos 30; nulo si la cohorte es pequeña.</param>
public sealed record MetricasProductoDto(
    DateTime GeneradoEn,
    int CohorteMinima,
    int CuentasNuevas7d,
    int EliminacionesEnCurso,
    int SugerenciasNuevas,
    int RegistrosPorCorreo7d,
    int? UsuariosActivos30d,
    int? Retencion30d,
    IReadOnlyList<PlanIniciadoDto> PlanesMasIniciados);
