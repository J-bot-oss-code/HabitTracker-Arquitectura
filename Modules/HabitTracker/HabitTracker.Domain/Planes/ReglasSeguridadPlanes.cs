using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Domain.Planes;

/// <summary>
/// Lo que el recomendador y la inscripción saben de la persona. Se arma en un solo punto (<see cref="Crear"/>): sin el
/// consentimiento <c>datosSalud</c> el cuestionario no se usa, aunque siga guardado.
/// </summary>
public sealed record SituacionPersona(
    PerfilUsuario Perfil,
    EvaluacionSalud Evaluacion,
    BloqueObjetivos? Objetivos,
    IReadOnlyList<CondicionSalud> Condiciones,
    bool UsaDatosDeSalud)
{
    /// <summary>Hay objetivos y revisión de seguridad: la recomendación ya no es solo por la jornada.</summary>
    public bool CuestionarioBasicoCompleto => UsaDatosDeSalud && Evaluacion.Completitud is { Objetivos: true, Seguridad: true };

    public static SituacionPersona Crear(PerfilUsuario perfil, CuestionarioSalud? cuestionario, bool usaDatosDeSalud)
    {
        ArgumentNullException.ThrowIfNull(perfil);
        var usado = usaDatosDeSalud ? cuestionario : null;
        var condiciones = (usado?.Condiciones?.Condiciones ?? [])
            .Where(c => c != CondicionSalud.Ninguna)
            .ToList();
        return new SituacionPersona(
            perfil,
            EvaluadorSalud.Evaluar(cuestionario, perfil, usaDatosDeSalud),
            usado?.Objetivos,
            condiciones,
            usaDatosDeSalud);
    }
}

public enum MotivoNoApto
{
    FueraDeRangoDeEdad,
    SexoDelPlan,
    SinPlanesDePeso,
    IntensidadNoRecomendada,
    ImpactoNoRecomendado,
    ZonaAEvitar,
    Contraindicado,
    ContieneAlergenos,
}

public sealed record RazonNoApto(MotivoNoApto Motivo, string Mensaje);

public sealed record EvaluacionPlan(IReadOnlyList<RazonNoApto> Razones, IReadOnlyList<string> Advertencias)
{
    public bool EsApto => Razones.Count == 0;
}

/// <summary>
/// Única definición de cuándo un plan no es apto para una persona (servicio de dominio puro). La usan el recomendador
/// (Fase 3) y la inscripción, que responde 400 <c>plan_no_apto</c> (Fase 4). Nunca diagnostica: los mensajes explican
/// la razón sin nombrar la condición de salud.
/// </summary>
public static class ReglasSeguridadPlanes
{
    public const string AvisoAguaMedico = "Consulta a tu médico cuánta agua te conviene tomar al día.";
    public const string AvisoConsultarMedico = "Antes de empezar, consulta a tu médico.";
    public const string AvisoCompletarEdad = "Agrega tu edad en tu perfil para confirmar que este plan es para ti.";
    public const string AvisoCompletarSexo = "Este plan está pensado para un sexo concreto; tu perfil no lo indica.";

    private static readonly CondicionSalud[] CondicionesConAvisoDeAgua =
        [CondicionSalud.Hipertension, CondicionSalud.EnfermedadCardiaca, CondicionSalud.EnfermedadRenal];

    public static EvaluacionPlan Evaluar(PlanPlantilla plan, SituacionPersona persona)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(persona);

        var razones = new List<RazonNoApto>();
        var advertencias = new List<string>();
        var restricciones = persona.Evaluacion.Restricciones;
        var perfil = persona.Perfil;

        // La edad y el sexo no son datos de salud: protegen aunque no haya consentimiento.
        if (perfil.Edad is { } edad)
        {
            if (edad < plan.EdadMinima || edad > plan.EdadMaxima)
                razones.Add(new(MotivoNoApto.FueraDeRangoDeEdad, $"Este plan es para personas de {plan.EdadMinima} a {plan.EdadMaxima} años."));
        }
        else if (plan.EdadMinima > PlanPlantilla.EdadMinimaPermitida || plan.EdadMaxima < PlanPlantilla.EdadMaximaPermitida)
        {
            advertencias.Add(AvisoCompletarEdad);
        }

        if (plan.Sexo != SexoPlan.Todos)
        {
            if (perfil.Sexo is { } sexo)
            {
                if (!Coincide(plan.Sexo, sexo))
                    razones.Add(new(MotivoNoApto.SexoDelPlan, "Este plan está pensado para otro sexo."));
            }
            else
            {
                advertencias.Add(AvisoCompletarSexo);
            }
        }

        if (plan.Area == AreaPlan.ControlDePeso && restricciones.Contains(RestriccionSalud.SinRestriccionCalorica))
            razones.Add(new(MotivoNoApto.SinPlanesDePeso, "Por lo que nos contaste, ahora no recomendamos planes de control de peso."));

        if ((restricciones.Contains(RestriccionSalud.SoloIntensidadBaja) && plan.Intensidad != IntensidadPlan.Baja)
            || (restricciones.Contains(RestriccionSalud.SinIntensidadAlta) && plan.Intensidad == IntensidadPlan.Alta))
            razones.Add(new(MotivoNoApto.IntensidadNoRecomendada, "La intensidad de este plan no es la recomendada para ti ahora."));

        if (plan.Impacto && restricciones.Contains(RestriccionSalud.SinImpacto))
            razones.Add(new(MotivoNoApto.ImpactoNoRecomendado, "Este plan tiene ejercicios de impacto que te conviene evitar."));

        if (ZonasAEvitar(restricciones).Intersect(plan.ZonasCorporales).Any())
            razones.Add(new(MotivoNoApto.ZonaAEvitar, "Este plan trabaja una zona que te conviene cuidar."));

        if (plan.Contraindicaciones.Intersect(persona.Condiciones).Any())
            razones.Add(new(MotivoNoApto.Contraindicado, "Por lo que nos contaste, este plan no es recomendable para ti."));

        if (plan.Alergenos.Intersect(persona.Evaluacion.AlergenosAEvitar).Any())
            razones.Add(new(MotivoNoApto.ContieneAlergenos, "Este plan incluye alimentos a los que indicaste alergia."));

        if (plan.IncluyeAgua && persona.Condiciones.Intersect(CondicionesConAvisoDeAgua).Any())
            advertencias.Add(AvisoAguaMedico);
        if (persona.Evaluacion.Nivel == NivelPrecaucion.ConsultarMedico)
            advertencias.Add(AvisoConsultarMedico);

        return new EvaluacionPlan(razones, advertencias);
    }

    private static bool Coincide(SexoPlan plan, Sexo sexo) =>
        (plan == SexoPlan.Mujer && sexo == Sexo.Mujer) || (plan == SexoPlan.Hombre && sexo == Sexo.Hombre);

    private static IEnumerable<ZonaCorporal> ZonasAEvitar(IReadOnlyList<RestriccionSalud> restricciones)
    {
        if (restricciones.Contains(RestriccionSalud.EvitarZonaRodilla))
            yield return ZonaCorporal.GluteosYPiernas;
        if (restricciones.Contains(RestriccionSalud.EvitarZonaEspaldaBaja))
            yield return ZonaCorporal.Espalda;
        if (restricciones.Contains(RestriccionSalud.EvitarZonaHombro))
            yield return ZonaCorporal.BrazosYHombros;
    }
}
