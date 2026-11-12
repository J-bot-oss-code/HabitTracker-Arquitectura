using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.DTOs;

/// <param name="Ancla">Cuándo lo hará: una sola ancla para el recordatorio del plan. Si no viene, la de su primera tarea.</param>
/// <param name="Hora">Hora local del recordatorio. Si viene, manda sobre el ancla.</param>
public sealed record InscribirseCommand(string Slug, Ancla? Ancla, TimeOnly? Hora);

/// <param name="MotivoPausa">Obligatorio al pausar.</param>
/// <param name="ConservarHabitos">
/// Al completar o cancelar: verdadero deja los hábitos activos y sueltos; falso los pausa y los deja en el historial del plan.
/// </param>
public sealed record CambiarEstadoPlanCommand(EstadoPlanUsuario NuevoEstado, MotivoPausa? MotivoPausa, bool ConservarHabitos);

/// <param name="Tipo">Solo <c>ModoSuave</c> o <c>Extender</c>: pausar es una transición.</param>
public sealed record AjustarPlanCommand(TipoAjuste Tipo);

public sealed record ConfigurarRecordatorioCommand(TimeOnly? Hora, bool RecordatoriosActivos);

public sealed record HabitoDePlanDto(
    Guid HabitoId,
    string Nombre,
    string? Descripcion,
    Periodicidad Periodicidad,
    decimal CantidadObjetivo,
    string Unidad,
    EstadoHabito Estado,
    string? VersionMinima,
    Ancla? Ancla,
    string? HoraSugerida);

public sealed record CumplimientoDto(int DiasCompletos, int DiasMinimos, int DiasElegibles, decimal PorcentajeCompleto, decimal PorcentajeConMinimo);

public sealed record ProgresoPlanDto(
    int Racha,
    int RachaMasLarga,
    CumplimientoDto Siete,
    CumplimientoDto Treinta,
    int SemanaActual,
    int SemanasTotales,
    int DiasRestantes,
    int DiasCumplidos,
    int DiasSinRegistroSeguidos,
    bool CumplidoHoy,
    IReadOnlyList<int> SemanasCelebradas,
    /// <summary>En qué se cuentan la racha y el cumplimiento: días (plan con tareas diarias) o semanas (plan semanal).</summary>
    UnidadSeguimiento Unidad);

public sealed record SugerenciaAjusteDto(IReadOnlyList<TipoAjuste> Opciones, int DiasSinRegistro);

/// <summary>El tip extra solo viaja en las semanas ya celebradas (5 de 7 días cumplidos).</summary>
public sealed record SemanaGuiaPlanDto(int Semana, string Texto, string? TipExtra);

public sealed record PlanUsuarioDto(
    Guid Id,
    string? Slug,
    string Nombre,
    OrigenPlan Origen,
    EstadoPlanUsuario Estado,
    MotivoPausa? MotivoPausa,
    int Semanas,
    int SemanasTotales,
    int Extensiones,
    DateTime FechaInicio,
    DateTime FechaFinPrevista,
    DateTime? ModoSuaveHasta,
    string? HoraRecordatorio,
    bool RecordatoriosActivos,
    IReadOnlyList<HabitoDePlanDto> Habitos,
    ProgresoPlanDto Progreso,
    SugerenciaAjusteDto? Sugerencia,
    IReadOnlyList<SemanaGuiaPlanDto> Guia);

// ---------- Hoy ----------

public enum EstadoTareaHoy
{
    Pendiente,
    Hecho,
    /// <summary>Hubo avance con la versión mínima y todavía no se llegó a la meta.</summary>
    Minimo,
}

public sealed record TareaHoyDto(
    Guid HabitoId,
    Guid PlanId,
    string PlanNombre,
    string Nombre,
    string? Instrucciones,
    Periodicidad Periodicidad,
    decimal CantidadObjetivo,
    string Unidad,
    Ancla? Ancla,
    string? HoraSugerida,
    string? VersionMinima,
    EstadoTareaHoy Estado,
    /// <summary>Lo avanzado en el periodo de la tarea: hoy si es diaria, esta semana si es semanal.</summary>
    decimal CantidadPeriodo,
    decimal Restante);

public sealed record PlanHoyDto(
    Guid PlanId,
    string Nombre,
    int SemanaActual,
    int SemanasTotales,
    int DiasRestantes,
    int Racha,
    SugerenciaAjusteDto? Sugerencia);

/// <param name="Mensaje">Uno al día, elegido por el tono del momento. Nulo si no hay un plan activo.</param>
/// <param name="CelebracionSemana">La semana recién cerrada con 5 de 7 días cumplidos, si hay una para celebrar.</param>
/// <param name="LogrosNuevos">Logros ganados que la persona todavía no vio celebrados.</param>
public sealed record HoyDto(
    DateOnly Fecha,
    IReadOnlyList<TareaHoyDto> Tareas,
    IReadOnlyList<PlanHoyDto> Planes,
    string? Mensaje,
    int? CelebracionSemana,
    string? TipExtra,
    IReadOnlyList<ClaveLogro> LogrosNuevos);

// ---------- Resumen de progreso ----------

public sealed record LogroDto(ClaveLogro Clave, DateTime ObtenidoEn, bool Visto);

public sealed record MesEnNumerosDto(int DiasCumplidos, int HabitosMantenidos, int Regresos);

public sealed record ResumenProgresoDto(
    int Racha,
    int RachaMasLarga,
    IReadOnlyList<LogroDto> Logros,
    MesEnNumerosDto Mes,
    string? Mensaje,
    int PlanesEnCurso);

// ---------- Descarga de datos ----------

public sealed record PausaExportadaDto(DateTime InicioUtc, DateTime? FinUtc, MotivoPausa Motivo);

public sealed record PlanExportadoDto(
    Guid Id,
    string? Slug,
    string Nombre,
    EstadoPlanUsuario Estado,
    DateTime FechaInicio,
    DateTime FechaFinPrevista,
    int Extensiones,
    DateTime? ModoSuaveHasta,
    string? HoraRecordatorio,
    bool RecordatoriosActivos,
    IReadOnlyList<PausaExportadaDto> Pausas);

public sealed record DatosPlanesDto(IReadOnlyList<PlanExportadoDto> Planes, IReadOnlyList<LogroDto> Logros);
