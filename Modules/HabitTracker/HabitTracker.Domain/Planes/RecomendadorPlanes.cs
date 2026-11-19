using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Domain.Planes;

/// <summary>
/// "Mi día de trabajo saludable": la puerta de entrada (P2). Mapa cerrado de la jornada a su variante; cada perfil
/// laboral tiene exactamente una.
/// </summary>
public static class VariantesMiDia
{
    public const string Oficina = "mi-dia-oficina";
    public const string TurnoNocturno = "mi-dia-turno-nocturno";
    public const string TrabajoFisico = "mi-dia-trabajo-fisico";

    public static IReadOnlyList<string> Slugs { get; } = [Oficina, TurnoNocturno, TrabajoFisico];

    public static string SlugPara(PerfilLaboral perfil) => perfil switch
    {
        PerfilLaboral.OficinaDiurna or PerfilLaboral.Remoto or PerfilLaboral.Estudiante or PerfilLaboral.SinEmpleo => Oficina,
        PerfilLaboral.TurnoNocturno or PerfilLaboral.TurnosRotativos => TurnoNocturno,
        PerfilLaboral.TrabajoFisico => TrabajoFisico,
        _ => throw new ArgumentOutOfRangeException(nameof(perfil), "Perfil laboral no válido."),
    };

    public static bool EsVariante(string slug) => Slugs.Contains(slug, StringComparer.Ordinal);
}

public sealed record PlanRecomendado(PlanPlantilla Plan, int Puntaje, IReadOnlyList<string> Motivos, IReadOnlyList<string> Advertencias);

public sealed record PlanNoApto(PlanPlantilla Plan, IReadOnlyList<RazonNoApto> Razones);

/// <param name="PersonalizadoConSalud">
/// Falso: se recomendó solo por la jornada (y la edad, si se conoce), porque falta el cuestionario o el consentimiento.
/// </param>
public sealed record Recomendaciones(
    IReadOnlyList<PlanRecomendado> Recomendados,
    IReadOnlyList<PlanNoApto> NoAptos,
    bool PersonalizadoConSalud);

/// <summary>
/// Ordena el catálogo para una persona (servicio de dominio puro). Sin cuestionario recomienda por la jornada y deja
/// primero lo que se empieza hoy; con cuestionario afina por objetivos, lugar y experiencia. La variante de "Mi día" de
/// su jornada va siempre primera, y las exclusiones de seguridad son de <see cref="ReglasSeguridadPlanes"/>.
/// </summary>
public static class RecomendadorPlanes
{
    public const string AvisoPreguntasDeSalud = "Antes de empezarlo te haremos unas preguntas de salud.";
    public const string AvisoNecesitaGimnasio = "Necesita un gimnasio.";

    private const int PuntosMiDia = 1000;
    private const int PuntosObjetivo = 30;
    private const int PuntosJornada = 20;
    private const int PuntosSinPreguntas = 10;
    private const int PuntosEdad = 10;
    private const int PuntosBasico = 5;
    private const int PenalizacionPreguntas = 10;
    private const int PenalizacionLugar = 15;
    private const int PenalizacionExperiencia = 10;

    public static Recomendaciones Recomendar(IEnumerable<PlanPlantilla> planes, SituacionPersona persona)
    {
        ArgumentNullException.ThrowIfNull(planes);
        ArgumentNullException.ThrowIfNull(persona);

        var personalizado = persona.CuestionarioBasicoCompleto;
        var miDia = VariantesMiDia.SlugPara(persona.Perfil.PerfilLaboral);
        var recomendados = new List<PlanRecomendado>();
        var noAptos = new List<PlanNoApto>();

        foreach (var plan in planes.Where(p => p.Activo))
        {
            // Las variantes de "Mi día" de otras jornadas no se recomiendan: siguen en el catálogo.
            if (VariantesMiDia.EsVariante(plan.Slug) && plan.Slug != miDia)
                continue;

            var evaluacion = ReglasSeguridadPlanes.Evaluar(plan, persona);
            if (!evaluacion.EsApto)
            {
                noAptos.Add(new PlanNoApto(plan, evaluacion.Razones));
                continue;
            }

            var (puntaje, motivos, advertencias) = Puntuar(plan, persona, personalizado, plan.Slug == miDia);
            advertencias.AddRange(evaluacion.Advertencias);
            recomendados.Add(new PlanRecomendado(plan, puntaje, motivos, advertencias));
        }

        var ordenados = recomendados
            .OrderByDescending(r => r.Puntaje)
            .ThenBy(r => r.Plan.Nivel)
            .ThenBy(r => r.Plan.Slug, StringComparer.Ordinal)
            .ToList();
        var noAptosOrdenados = noAptos.OrderBy(n => n.Plan.Slug, StringComparer.Ordinal).ToList();
        return new Recomendaciones(ordenados, noAptosOrdenados, personalizado);
    }

    private static (int Puntaje, List<string> Motivos, List<string> Advertencias) Puntuar(
        PlanPlantilla plan, SituacionPersona persona, bool personalizado, bool esMiDia)
    {
        var puntaje = 0;
        var motivos = new List<string>();
        var advertencias = new List<string>();
        var perfil = persona.Perfil;

        if (esMiDia)
        {
            puntaje += PuntosMiDia;
            motivos.Add($"Pensado para {FraseJornada(perfil.PerfilLaboral)}.");
        }
        else if (plan.PerfilesLaborales.Contains(perfil.PerfilLaboral))
        {
            puntaje += PuntosJornada;
            motivos.Add("Encaja con tu jornada.");
        }

        if (perfil.Edad is not null && (plan.EdadMinima > PlanPlantilla.EdadMinimaPermitida || plan.EdadMaxima < PlanPlantilla.EdadMaximaPermitida))
        {
            puntaje += PuntosEdad;
            motivos.Add("Pensado para tu edad.");
        }

        if (personalizado && persona.Objetivos is { } objetivos)
        {
            foreach (var objetivo in objetivos.Objetivos.Where(o => AreaDe(o) == plan.Area))
            {
                puntaje += PuntosObjetivo;
                motivos.Add($"Va con tu objetivo: {FraseObjetivo(objetivo)}.");
            }

            if (plan.Lugar == Lugar.Gimnasio && objetivos.Lugar != Lugar.Gimnasio)
            {
                puntaje -= PenalizacionLugar;
                advertencias.Add(AvisoNecesitaGimnasio);
            }

            if (objetivos.Experiencia == Experiencia.Ninguna && plan.Intensidad != IntensidadPlan.Baja)
                puntaje -= PenalizacionExperiencia;
        }
        else
        {
            // Sin cuestionario, primero lo que se empieza hoy en 3 toques (P1).
            if (!plan.RequiereCuestionario)
            {
                puntaje += PuntosSinPreguntas;
                if (!esMiDia)
                    motivos.Add("Lo empiezas hoy, sin preguntas de salud.");
            }
            else
            {
                puntaje -= PenalizacionPreguntas;
                advertencias.Add(AvisoPreguntasDeSalud);
            }

            if (plan.Nivel == NivelPlan.Basico)
                puntaje += PuntosBasico;
        }

        if (personalizado && plan.RequiereCuestionario && !persona.Evaluacion.Completitud.Condiciones
            && (plan.Contraindicaciones.Count > 0 || plan.Alergenos.Count > 0))
            advertencias.Add(AvisoPreguntasDeSalud);

        return (puntaje, motivos, advertencias);
    }

    /// <summary>A qué área del catálogo apunta cada objetivo del cuestionario.</summary>
    public static AreaPlan AreaDe(Objetivo objetivo) => objetivo switch
    {
        Objetivo.PerderPeso => AreaPlan.ControlDePeso,
        Objetivo.GanarMasaMuscular or Objetivo.DefinirTonificar => AreaPlan.Gimnasio,
        Objetivo.ReducirEstres => AreaPlan.Estres,
        Objetivo.MejorarConcentracion => AreaPlan.Concentracion,
        Objetivo.DormirMejor => AreaPlan.Descanso,
        Objetivo.Hidratarme => AreaPlan.Hidratacion,
        Objetivo.MasEnergia => AreaPlan.Energia,
        Objetivo.PosturaYMovilidad => AreaPlan.Movilidad,
        _ => throw new ArgumentOutOfRangeException(nameof(objetivo), "Objetivo no válido."),
    };

    private static string FraseJornada(PerfilLaboral perfil) => perfil switch
    {
        PerfilLaboral.OficinaDiurna => "tu jornada de oficina",
        PerfilLaboral.Remoto => "tu trabajo remoto",
        PerfilLaboral.Estudiante => "tus días de estudio",
        PerfilLaboral.SinEmpleo => "tu día a día",
        PerfilLaboral.TurnoNocturno => "tu turno nocturno",
        PerfilLaboral.TurnosRotativos => "tus turnos rotativos",
        PerfilLaboral.TrabajoFisico => "tu trabajo físico o de pie",
        _ => "tu jornada",
    };

    private static string FraseObjetivo(Objetivo objetivo) => objetivo switch
    {
        Objetivo.PerderPeso => "cuidar tu peso",
        Objetivo.GanarMasaMuscular => "ganar masa muscular",
        Objetivo.DefinirTonificar => "definir y tonificar",
        Objetivo.ReducirEstres => "reducir el estrés",
        Objetivo.MejorarConcentracion => "concentrarte mejor",
        Objetivo.DormirMejor => "dormir mejor",
        Objetivo.Hidratarme => "hidratarte",
        Objetivo.MasEnergia => "tener más energía",
        Objetivo.PosturaYMovilidad => "postura y movilidad",
        _ => "tu objetivo",
    };
}
