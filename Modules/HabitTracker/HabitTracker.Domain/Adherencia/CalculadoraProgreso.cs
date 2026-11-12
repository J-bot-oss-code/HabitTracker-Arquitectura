using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Domain.Adherencia;

/// <summary>Cómo fue un día (o una semana) del plan.</summary>
public enum EstadoDia
{
    SinRegistro,
    /// <summary>Hubo avance, pero no en todas las tareas diarias (o, en un plan semanal, menos sesiones que la meta).</summary>
    Parcial,
    /// <summary>Todo hecho, con la versión mínima en alguna tarea (y sin modo suave).</summary>
    Minimo,
    Completo,
    /// <summary>Un día de una pausa justificada sin avance: no cuenta ni corta la racha.</summary>
    EnPausa,
}

/// <summary>
/// En qué se mide la constancia de un plan. Un plan con tareas diarias se mide en días. Uno que solo tiene tareas semanales
/// (por ejemplo, tres salidas a correr por semana) se mide en semanas: ahí un día sin sesión es lo esperado, no una falla.
/// </summary>
public enum UnidadSeguimiento
{
    Dias,
    Semanas,
}

/// <summary>Un avance, reducido a lo que la calculadora necesita. Nunca lleva peso ni medidas (P4).</summary>
public sealed record RegistroDeAvance(Guid HabitoId, DateTime FechaUtc, bool EsVersionMinima);

public sealed record TareaDelPlan(Guid HabitoId, Periodicidad Periodicidad, decimal CantidadObjetivo = 1);

public sealed record PausaDelPlan(DateTime InicioUtc, DateTime? FinUtc);

public sealed record ParametrosProgreso(
    DateTime InicioPlanUtc,
    DateTime FinPrevistoUtc,
    int SemanasTotales,
    TimeZoneInfo Zona,
    DateTime AhoraUtc,
    DateTime? ModoSuaveHastaUtc,
    IReadOnlyList<PausaDelPlan> Pausas);

/// <summary>
/// Completos y con la versión mínima, por separado, sobre las unidades que cuentan de la ventana (días, o semanas en un
/// plan semanal; ver <see cref="ResultadoProgreso.Unidad"/>).
/// </summary>
public sealed record Cumplimiento(int DiasCompletos, int DiasMinimos, int DiasElegibles)
{
    public decimal PorcentajeCompleto => Porcentaje(DiasCompletos);
    public decimal PorcentajeConMinimo => Porcentaje(DiasCompletos + DiasMinimos);

    private decimal Porcentaje(int unidades) => DiasElegibles == 0 ? 0m : Math.Round(unidades * 100m / DiasElegibles, 1);
}

/// <param name="Unidad">En qué se cuentan <c>Racha</c>, <c>RachaMasLarga</c> y los <c>Cumplimiento</c>: días o semanas.</param>
public sealed record ResultadoProgreso(
    DateOnly HoyLocal,
    int Racha,
    int RachaMasLarga,
    Cumplimiento Siete,
    Cumplimiento Treinta,
    int SemanaActual,
    int SemanasTotales,
    int DiasRestantes,
    /// <summary>Días del plan que cuentan (sin los de una pausa sin avance), hasta hoy.</summary>
    int DiasTranscurridos,
    int DiasCumplidos,
    /// <summary>Un plan diario: días seguidos sin avance. Uno semanal: lleva una semana entera o más sin sesión, o 0.</summary>
    int DiasSinRegistroSeguidos,
    int DiasSinRegistroUltimos7,
    bool CumplidoHoy,
    /// <summary>Semanas ya cerradas con la meta cumplida (5 de 7 días en un plan diario): desbloquean su tip extra.</summary>
    IReadOnlyList<int> SemanasCelebradas,
    /// <summary>Días en que la persona volvió tras 2 días (o 2 semanas) sin registro o más.</summary>
    IReadOnlyList<DateOnly> Regresos,
    UnidadSeguimiento Unidad = UnidadSeguimiento.Dias);

/// <summary>
/// Única definición de la adherencia de un plan (servicio de dominio puro, P3). El día es el <b>día local</b> de la persona,
/// no el día UTC. Un día "cumplido" tiene al menos un avance en los hábitos del plan; es "completo" si se hicieron todas
/// las tareas diarias (en modo suave la versión mínima cuenta como completa). Un día sin registro no corta la racha y dos
/// seguidos sí; los días de una pausa justificada no cuentan ni la cortan. Hoy, mientras no tenga avance, no se da por
/// fallado. Un plan que solo tiene tareas semanales se mide por semanas, con la misma tolerancia.
/// </summary>
public static class CalculadoraProgreso
{
    public const int DiasSeguidosQueCortan = 2;
    public const int DiasCumplidosParaCelebrar = 5;
    private const int SemanasDeLaVentanaLarga = 4;

    public static DateOnly DiaLocal(TimeZoneInfo zona, DateTime fechaUtc)
    {
        ArgumentNullException.ThrowIfNull(zona);
        var utc = fechaUtc.Kind == DateTimeKind.Utc ? fechaUtc : DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, zona));
    }

    /// <summary>El lunes de la semana local del día.</summary>
    public static DateOnly InicioDeSemana(DateOnly dia) => dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));

    public static ResultadoProgreso Calcular(
        IReadOnlyList<TareaDelPlan> tareas, IReadOnlyList<RegistroDeAvance> registros, ParametrosProgreso p)
    {
        ArgumentNullException.ThrowIfNull(tareas);
        ArgumentNullException.ThrowIfNull(registros);
        ArgumentNullException.ThrowIfNull(p);

        var zona = p.Zona;
        var hoy = DiaLocal(zona, p.AhoraUtc);
        var inicio = DiaLocal(zona, p.InicioPlanUtc);
        var finPlan = DiaLocal(zona, p.FinPrevistoUtc);
        // El plan dura hasta el día anterior a la fecha de fin prevista (4 semanas = 28 días, no 29).
        var ultimoDelPlan = finPlan.AddDays(-1);
        var ultimo = hoy < ultimoDelPlan ? hoy : ultimoDelPlan;
        if (ultimo < inicio)
            ultimo = inicio;

        var diarias = tareas.Where(t => t.Periodicidad == Periodicidad.Diaria).Select(t => t.HabitoId).ToHashSet();
        var porDia = registros.ToLookup(r => DiaLocal(zona, r.FechaUtc));

        var dias = new List<(DateOnly Dia, EstadoDia Estado)>();
        for (var d = inicio; d <= ultimo; d = d.AddDays(1))
            dias.Add((d, EstadoDelDia(d, porDia[d].ToList(), diarias, hoy, p)));

        var activos = dias.Where(x => x.Estado != EstadoDia.EnPausa).ToList();
        var semana = Math.Clamp((activos.Count - 1) / PlanUsuario.DiasPorSemana + 1, 1, Math.Max(1, p.SemanasTotales));
        var cumplidoHoy = dias.Count > 0 && dias[^1].Dia == hoy && EsCumplido(dias[^1].Estado);
        var restantes = Math.Max(0, finPlan.DayNumber - hoy.DayNumber);

        var soloSemanales = tareas.Count > 0 && diarias.Count == 0;
        return soloSemanales
            ? CalcularPorSemanas(tareas, dias, activos, inicio, hoy, finPlan, semana, cumplidoHoy, restantes, p)
            : CalcularPorDias(dias, activos, ultimo, hoy, finPlan, semana, cumplidoHoy, restantes, p);
    }

    // ---------- Plan con tareas diarias: se mide en días ----------

    private static ResultadoProgreso CalcularPorDias(
        List<(DateOnly Dia, EstadoDia Estado)> dias, List<(DateOnly Dia, EstadoDia Estado)> activos, DateOnly ultimo,
        DateOnly hoy, DateOnly finPlan, int semana, bool cumplidoHoy, int restantes, ParametrosProgreso p)
    {
        // Racha: un día sin registro no corta, dos seguidos sí. Hoy sin avance todavía no se da por fallado.
        int racha = 0, rachaMasLarga = 0, hueco = 0, cumplidos = 0;
        var haCumplidoAntes = false;
        var regresos = new List<DateOnly>();
        foreach (var (dia, estado) in activos)
        {
            if (EsCumplido(estado))
            {
                if (hueco >= DiasSeguidosQueCortan)
                {
                    racha = 1;
                    if (haCumplidoAntes)
                        regresos.Add(dia);
                }
                else
                {
                    racha++;
                }

                hueco = 0;
                cumplidos++;
                haCumplidoAntes = true;
                rachaMasLarga = Math.Max(rachaMasLarga, racha);
            }
            else if (dia != hoy)
            {
                hueco++;
            }
        }

        if (hueco >= DiasSeguidosQueCortan)
            racha = 0;

        var ultimosSiete = dias.Where(x => x.Dia > ultimo.AddDays(-7)).ToList();
        return new ResultadoProgreso(
            hoy, racha, rachaMasLarga,
            Resumir(ultimosSiete, hoy),
            Resumir(dias.Where(x => x.Dia > ultimo.AddDays(-30)).ToList(), hoy),
            semana, p.SemanasTotales, restantes, activos.Count, cumplidos, hueco,
            ultimosSiete.Count(x => x.Estado == EstadoDia.SinRegistro && x.Dia != hoy),
            cumplidoHoy,
            SemanasCerradasYCelebradas(activos, p.SemanasTotales, hoy, finPlan),
            regresos);
    }

    // ---------- Plan solo con tareas semanales: se mide en semanas ----------

    private static ResultadoProgreso CalcularPorSemanas(
        IReadOnlyList<TareaDelPlan> tareas, List<(DateOnly Dia, EstadoDia Estado)> dias,
        List<(DateOnly Dia, EstadoDia Estado)> activos, DateOnly inicio, DateOnly hoy, DateOnly finPlan, int semanaDelPlan,
        bool cumplidoHoy, int restantes, ParametrosProgreso p)
    {
        // La meta de la semana: las sesiones de la tarea más exigente (3 salidas, 3 sesiones de fuerza).
        var meta = Math.Clamp((int)Math.Ceiling(tareas.Max(t => t.CantidadObjetivo)), 1, PlanUsuario.DiasPorSemana);
        var semanasDelPlan = dias
            .GroupBy(x => InicioDeSemana(x.Dia))
            .OrderBy(g => g.Key)
            .Select(g => (Lunes: g.Key, Estado: EstadoDeLaSemana(g.ToList(), meta), Dias: g.ToList()))
            .ToList();

        int racha = 0, rachaMasLarga = 0, hueco = 0;
        var haCumplidoAntes = false;
        var regresos = new List<DateOnly>();
        var semanaActual = InicioDeSemana(hoy);
        foreach (var (lunes, estado, delaSemana) in semanasDelPlan.Where(s => s.Estado != EstadoDia.EnPausa))
        {
            if (EsCumplido(estado))
            {
                if (hueco >= DiasSeguidosQueCortan)
                {
                    racha = 1;
                    if (haCumplidoAntes)
                        regresos.Add(delaSemana.First(d => EsCumplido(d.Estado)).Dia);
                }
                else
                {
                    racha++;
                }

                hueco = 0;
                haCumplidoAntes = true;
                rachaMasLarga = Math.Max(rachaMasLarga, racha);
            }
            else if (lunes != semanaActual)
            {
                hueco++;
            }
        }

        if (hueco >= DiasSeguidosQueCortan)
            racha = 0;

        // "Días sin registro": en un plan semanal solo cuenta una semana entera (o más) sin una sola sesión, contada desde la
        // última sesión o, si nunca hubo una, desde el inicio del plan.
        var ultimoAvance = activos.Where(x => EsCumplido(x.Estado)).Select(x => x.Dia).Cast<DateOnly?>().LastOrDefault();
        var diasDesdeLaUltima = hoy.DayNumber - (ultimoAvance ?? inicio).DayNumber;
        var semanaSinSesion = diasDesdeLaUltima >= PlanUsuario.DiasPorSemana;
        var sinRegistroSeguidos = semanaSinSesion ? diasDesdeLaUltima : 0;

        return new ResultadoProgreso(
            hoy, racha, rachaMasLarga,
            ResumirSemanas(semanasDelPlan.Select(s => (s.Lunes, s.Estado)).ToList(), semanaActual, 1),
            ResumirSemanas(semanasDelPlan.Select(s => (s.Lunes, s.Estado)).ToList(), semanaActual, SemanasDeLaVentanaLarga),
            semanaDelPlan, p.SemanasTotales, restantes, activos.Count, activos.Count(x => EsCumplido(x.Estado)),
            sinRegistroSeguidos, semanaSinSesion ? PlanUsuario.DiasPorSemana : 0, cumplidoHoy,
            SemanasCalendarioCelebradas(semanasDelPlan.Select(s => (s.Lunes, s.Estado)).ToList(), p.SemanasTotales, hoy),
            regresos, UnidadSeguimiento.Semanas);
    }

    // La semana se juzga por sus sesiones (días con avance): completa con la meta cumplida, con la versión mínima si algunas
    // fueron mínimas, parcial con alguna sesión y sin registro con ninguna. Una semana entera en pausa no cuenta.
    private static EstadoDia EstadoDeLaSemana(List<(DateOnly Dia, EstadoDia Estado)> dias, int meta)
    {
        if (dias.All(x => x.Estado == EstadoDia.EnPausa))
            return EstadoDia.EnPausa;

        var completas = dias.Count(x => x.Estado == EstadoDia.Completo);
        var conAlgo = dias.Count(x => EsCumplido(x.Estado));
        if (completas >= meta)
            return EstadoDia.Completo;
        if (conAlgo >= meta)
            return EstadoDia.Minimo;
        return conAlgo >= 1 ? EstadoDia.Parcial : EstadoDia.SinRegistro;
    }

    // La ventana son las últimas N semanas elegibles: ni las de pausa ni la semana en curso si todavía no tiene sesiones (un
    // lunes por la mañana el resumen sigue mostrando la semana que acaba de cerrar, no un vacío).
    private static Cumplimiento ResumirSemanas(List<(DateOnly Lunes, EstadoDia Estado)> semanas, DateOnly semanaActual, int ultimas)
    {
        var elegibles = semanas
            .Where(s => s.Estado != EstadoDia.EnPausa && !(s.Lunes == semanaActual && s.Estado == EstadoDia.SinRegistro))
            .TakeLast(ultimas)
            .ToList();
        return new Cumplimiento(
            elegibles.Count(s => s.Estado == EstadoDia.Completo),
            elegibles.Count(s => s.Estado == EstadoDia.Minimo),
            elegibles.Count);
    }

    // La semana w del plan es la w-ésima semana de calendario desde el inicio; se celebra al cerrar (pasado su domingo) con la
    // meta cumplida.
    private static List<int> SemanasCalendarioCelebradas(
        List<(DateOnly Lunes, EstadoDia Estado)> semanas, int semanasTotales, DateOnly hoy)
    {
        var celebradas = new List<int>();
        for (var i = 0; i < semanas.Count && i < semanasTotales; i++)
            if (semanas[i].Lunes.AddDays(PlanUsuario.DiasPorSemana - 1) < hoy && semanas[i].Estado == EstadoDia.Completo)
                celebradas.Add(i + 1);
        return celebradas;
    }

    // ---------- Común ----------

    private static EstadoDia EstadoDelDia(
        DateOnly dia, IReadOnlyList<RegistroDeAvance> delDia, HashSet<Guid> diarias, DateOnly hoy, ParametrosProgreso p)
    {
        // Un día con avance se evalúa como cualquier otro, aunque la persona estuviera en pausa: el esfuerzo es real.
        if (delDia.Count == 0)
            return EnPausa(dia, hoy, p) ? EstadoDia.EnPausa : EstadoDia.SinRegistro;

        var suave = ModoSuaveActivo(dia, p);

        // Un plan sin tareas diarias: cualquier avance cumple el día.
        if (diarias.Count == 0)
            return delDia.All(r => r.EsVersionMinima) && !suave ? EstadoDia.Minimo : EstadoDia.Completo;

        var todasConAvance = true;
        var todasCompletas = true;
        foreach (var tarea in diarias)
        {
            var deLaTarea = delDia.Where(r => r.HabitoId == tarea).ToList();
            if (deLaTarea.Count == 0)
            {
                todasConAvance = false;
                todasCompletas = false;
            }
            else if (deLaTarea.All(r => r.EsVersionMinima))
            {
                todasCompletas = false;
            }
        }

        if (!todasConAvance)
            return EstadoDia.Parcial;
        return todasCompletas || suave ? EstadoDia.Completo : EstadoDia.Minimo;
    }

    // Los días de una pausa son los que Reanudar agrega al final del plan: del día en que empezó a antes del día en que se
    // reanudó. Una pausa abierta cubre desde su inicio hasta hoy.
    private static bool EnPausa(DateOnly dia, DateOnly hoy, ParametrosProgreso p) =>
        p.Pausas.Any(x =>
        {
            var inicio = DiaLocal(p.Zona, x.InicioUtc);
            return dia >= inicio && (x.FinUtc is not { } fin ? dia <= hoy : dia < DiaLocal(p.Zona, fin));
        });

    private static bool ModoSuaveActivo(DateOnly dia, ParametrosProgreso p) =>
        p.ModoSuaveHastaUtc is { } hasta
        && dia <= DiaLocal(p.Zona, hasta)
        && dia >= DiaLocal(p.Zona, hasta.AddDays(-PlanUsuario.DiasModoSuave));

    private static bool EsCumplido(EstadoDia estado) => estado is EstadoDia.Parcial or EstadoDia.Minimo or EstadoDia.Completo;

    private static Cumplimiento Resumir(List<(DateOnly Dia, EstadoDia Estado)> ventana, DateOnly hoy)
    {
        // Cuentan los días que no fueron de pausa; el de hoy, solo si ya tiene avance (sigue en curso).
        var elegibles = ventana
            .Where(x => x.Estado != EstadoDia.EnPausa && !(x.Dia == hoy && x.Estado == EstadoDia.SinRegistro))
            .ToList();
        return new Cumplimiento(
            elegibles.Count(x => x.Estado == EstadoDia.Completo),
            elegibles.Count(x => x.Estado == EstadoDia.Minimo),
            elegibles.Count);
    }

    // Las semanas del plan se cuentan en días que cuentan (los de pausa no avanzan el programa).
    private static List<int> SemanasCerradasYCelebradas(
        List<(DateOnly Dia, EstadoDia Estado)> activos, int semanasTotales, DateOnly hoy, DateOnly finPlan)
    {
        var celebradas = new List<int>();
        for (var semana = 1; semana <= semanasTotales; semana++)
        {
            var desde = (semana - 1) * PlanUsuario.DiasPorSemana;
            var hasta = semana * PlanUsuario.DiasPorSemana;
            var cerrada = hasta < activos.Count || (hasta == activos.Count && hoy >= finPlan);
            if (!cerrada)
                break;

            if (activos.Skip(desde).Take(PlanUsuario.DiasPorSemana).Count(x => EsCumplido(x.Estado)) >= DiasCumplidosParaCelebrar)
                celebradas.Add(semana);
        }

        return celebradas;
    }
}
