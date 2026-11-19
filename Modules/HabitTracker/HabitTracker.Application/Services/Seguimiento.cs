using HabitTracker.Application.DTOs;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Services;

/// <summary>El progreso de un plan junto con lo que se necesitó para calcularlo (lo comparten Hoy, el resumen y los logros).</summary>
public sealed record ProgresoDePlan(PlanUsuario Plan, IReadOnlyList<Habito> Habitos, ResultadoProgreso Progreso);

/// <summary>
/// Pegamento de la capa de aplicación entre las entidades y las calculadoras de dominio, y entre estas y los DTO. No decide
/// nada de negocio: la adherencia es de <see cref="CalculadoraProgreso"/> y las reglas del plan, de <see cref="PlanUsuario"/>.
/// </summary>
internal static class Seguimiento
{
    /// <summary>
    /// Zona horaria de la persona. Sin perfil, o con una zona que el sistema no conoce, se usa UTC: un plan nunca se rompe
    /// por no poder resolver la hora local.
    /// </summary>
    public static TimeZoneInfo Zona(PerfilUsuario? perfil)
    {
        if (perfil is null)
            return TimeZoneInfo.Utc;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(perfil.ZonaHoraria);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }

    public static ResultadoProgreso Progreso(PlanUsuario plan, IReadOnlyList<Habito> habitosDelPlan, TimeZoneInfo zona, DateTime ahoraUtc) =>
        CalculadoraProgreso.Calcular(
            // Cuentan las tareas que la persona sigue: las activas y las que el plan pausó con él. Una que la persona
            // pausó a mano (o completó) ya no puede registrar avance y dejaría todos los días "parciales" para siempre.
            [.. habitosDelPlan
                .Where(h => h.Estado == EstadoHabito.Activo || (h.Estado == EstadoHabito.Pausado && h.PausadoPorPlan))
                .Select(h => new TareaDelPlan(h.Id, h.Periodicidad, h.CantidadObjetivo))],
            [.. habitosDelPlan.SelectMany(h => h.Registros.Select(r => new RegistroDeAvance(h.Id, r.Fecha, r.EsVersionMinima)))],
            new ParametrosProgreso(
                plan.FechaInicio,
                plan.FechaFinPrevista,
                plan.SemanasTotales,
                zona,
                ahoraUtc,
                plan.ModoSuaveHasta,
                [.. plan.Pausas.Select(p => new PausaDelPlan(p.InicioUtc, p.FinUtc))]));

    public static IReadOnlyList<Habito> HabitosDe(PlanUsuario plan, IReadOnlyList<Habito> todos) =>
        [.. todos.Where(h => h.PlanUsuarioId == plan.Id)];

    public static string? Hora(Ancla? ancla, JornadaPersona? jornada) =>
        ancla is { } a && jornada is not null ? ResolvedorAncla.Resolver(a, jornada).ToString("HH:mm") : null;

    public static PlanUsuarioDto ADto(
        PlanUsuario plan, IReadOnlyList<Habito> habitosDelPlan, PlanPlantilla? plantilla, PerfilUsuario? perfil, DateTime ahoraUtc)
    {
        var progreso = Progreso(plan, habitosDelPlan, Zona(perfil), ahoraUtc);
        var jornada = perfil is null ? null : JornadaPersona.Desde(perfil);
        var sugerencia = SugeridorAjuste.Sugerir(progreso, plan.Estado, plan.Extensiones, plan.EnModoSuave(ahoraUtc));

        return new PlanUsuarioDto(
            plan.Id, plan.Slug, plan.Nombre, plan.Origen, plan.Estado, plan.MotivoPausa, plan.Semanas, plan.SemanasTotales,
            plan.Extensiones, plan.FechaInicio, plan.FechaFinPrevista, plan.ModoSuaveHasta,
            plan.HoraRecordatorio?.ToString("HH:mm"), plan.RecordatoriosActivos,
            [.. habitosDelPlan.Select(h => new HabitoDePlanDto(
                h.Id, h.Nombre, h.Descripcion, h.Periodicidad, h.CantidadObjetivo, h.Unidad, h.Estado, h.VersionMinima,
                h.Ancla, Hora(h.Ancla, jornada)))],
            ProgresoDto(progreso),
            sugerencia is null ? null : new SugerenciaAjusteDto(sugerencia.Opciones, sugerencia.DiasSinRegistro),
            Guia(plantilla, progreso.SemanasCelebradas));
    }

    public static ProgresoPlanDto ProgresoDto(ResultadoProgreso r) => new(
        r.Racha, r.RachaMasLarga, CumplimientoDto(r.Siete), CumplimientoDto(r.Treinta), r.SemanaActual, r.SemanasTotales,
        r.DiasRestantes, r.DiasCumplidos, r.DiasSinRegistroSeguidos, r.CumplidoHoy, r.SemanasCelebradas, r.Unidad);

    private static CumplimientoDto CumplimientoDto(Cumplimiento c) =>
        new(c.DiasCompletos, c.DiasMinimos, c.DiasElegibles, c.PorcentajeCompleto, c.PorcentajeConMinimo);

    /// <summary>La guía semanal; el tip extra solo en las semanas que la persona ya celebró.</summary>
    private static IReadOnlyList<SemanaGuiaPlanDto> Guia(PlanPlantilla? plantilla, IReadOnlyList<int> celebradas) =>
        plantilla is null
            ? []
            : [.. plantilla.GuiaSemanal.Select(g => new SemanaGuiaPlanDto(g.Semana, g.Texto, celebradas.Contains(g.Semana) ? g.TipExtra : null))];
}
