using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Application.DTOs;

/// <summary>Filtros opcionales de <c>GET /api/plans</c>. Nulo: sin filtrar.</summary>
public sealed record FiltrosPlanes(AreaPlan? Area, NivelPlan? Nivel, PerfilLaboral? PerfilLaboral);

/// <summary>Tarjeta de un plan en el catálogo público.</summary>
public sealed record PlanResumenDto(
    string Slug,
    string Nombre,
    string Resumen,
    AreaPlan Area,
    NivelPlan Nivel,
    IntensidadPlan Intensidad,
    int Semanas,
    SexoPlan Sexo,
    int EdadMinima,
    int EdadMaxima,
    IReadOnlyList<PerfilLaboral> PerfilesLaborales,
    IReadOnlyList<string> Beneficios,
    bool RequiereCuestionario);

public sealed record TareaPlantillaDto(
    int Orden,
    string Nombre,
    string Instrucciones,
    Periodicidad Periodicidad,
    decimal Cantidad,
    string Unidad,
    string VersionMinima,
    Ancla AnclaSugerida);

/// <summary>Semana de la guía en el detalle público. El tip extra no viaja: se desbloquea al seguir el plan (Fase 4).</summary>
public sealed record SemanaGuiaDto(int Semana, string Texto);

public sealed record PlanDetalleDto(
    string Slug,
    string Nombre,
    string Resumen,
    string Descripcion,
    AreaPlan Area,
    NivelPlan Nivel,
    IntensidadPlan Intensidad,
    int Semanas,
    SexoPlan Sexo,
    int EdadMinima,
    int EdadMaxima,
    IReadOnlyList<PerfilLaboral> PerfilesLaborales,
    Lugar? Lugar,
    bool Impacto,
    IReadOnlyList<ZonaCorporal> ZonasCorporales,
    IReadOnlyList<CondicionSalud> Contraindicaciones,
    IReadOnlyList<Alergia> Alergenos,
    IReadOnlyList<string> Beneficios,
    string AvisoSeguridad,
    bool RequiereCuestionario,
    IReadOnlyList<TareaPlantillaDto> Tareas,
    IReadOnlyList<SemanaGuiaDto> GuiaSemanal);

public sealed record PlanRecomendadoDto(PlanResumenDto Plan, IReadOnlyList<string> Motivos, IReadOnlyList<string> Advertencias);

public sealed record RazonNoAptoDto(MotivoNoApto Motivo, string Mensaje);

public sealed record PlanNoAptoDto(PlanResumenDto Plan, IReadOnlyList<RazonNoAptoDto> Razones);

/// <param name="Recomendados">Ordenados: la variante de "Mi día" de la jornada va primera.</param>
/// <param name="NoAptos">Planes que se muestran como "No recomendado para ti", con su razón.</param>
/// <param name="PersonalizadoConSalud">Falso: se recomendó por la jornada; el frontend invita a responder el cuestionario.</param>
public sealed record RecomendacionesDto(
    IReadOnlyList<PlanRecomendadoDto> Recomendados,
    IReadOnlyList<PlanNoAptoDto> NoAptos,
    bool PersonalizadoConSalud);
