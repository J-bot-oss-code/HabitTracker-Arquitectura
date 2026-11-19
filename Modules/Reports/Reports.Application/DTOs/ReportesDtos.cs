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
