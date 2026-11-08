using HabitTracker.Domain.Perfil;

namespace HabitTracker.Domain.Salud;

public enum NivelPrecaucion
{
    /// <summary>Sin señales de alerta en lo que la persona respondió.</summary>
    Normal,
    /// <summary>Alguna condición pide adaptar los planes (por ejemplo, sin impacto).</summary>
    Precaucion,
    /// <summary>Algún "sí" en la revisión de seguridad: solo intensidad baja y "consulta a tu médico".</summary>
    ConsultarMedico,
}

/// <summary>Restricciones derivadas que aplican los planes. Nunca son un diagnóstico.</summary>
public enum RestriccionSalud
{
    SoloIntensidadBaja,
    SinIntensidadAlta,
    SinImpacto,
    SinRestriccionCalorica,
    EvitarZonaRodilla,
    EvitarZonaEspaldaBaja,
    EvitarZonaHombro,
}

/// <summary>Qué bloques del cuestionario están respondidos.</summary>
public sealed record CompletitudCuestionario(bool Objetivos, bool Seguridad, bool Condiciones);

public sealed record EvaluacionSalud(
    NivelPrecaucion Nivel,
    IReadOnlyList<RestriccionSalud> Restricciones,
    IReadOnlyList<Alergia> AlergenosAEvitar,
    CompletitudCuestionario Completitud);

/// <summary>Lo que un plan necesita saber de la salud de la persona antes de empezar (lo define el catálogo, Fase 3).</summary>
public sealed record RequisitosSaludPlan(bool RequiereCuestionario, bool TieneAlergenos, bool TieneContraindicaciones);

/// <summary>
/// Única definición de las reglas de salud del módulo (servicio de dominio puro). La usan el recomendador, la inscripción
/// en un plan y los planes propios. Traduce respuestas a restricciones y avisos; no diagnostica.
/// </summary>
public static class EvaluadorSalud
{
    public const int EdadAdulta = 18;
    public const decimal ImcBajoPeso = 18.5m;

    public static EvaluacionSalud Evaluar(CuestionarioSalud? cuestionario, PerfilUsuario? perfil) =>
        Evaluar(cuestionario, perfil, usarDatosDeSalud: true);

    /// <summary>
    /// Con <paramref name="usarDatosDeSalud"/> en falso (la persona retiró el consentimiento <c>datosSalud</c>) se evalúa
    /// como si no hubiera cuestionario ni IMC: sus datos no se borran, pero dejan de usarse. La edad no es un dato de salud
    /// y sigue protegiendo a los menores.
    /// </summary>
    public static EvaluacionSalud Evaluar(CuestionarioSalud? cuestionario, PerfilUsuario? perfil, bool usarDatosDeSalud)
    {
        if (!usarDatosDeSalud)
            cuestionario = null;

        var restricciones = new SortedSet<RestriccionSalud>();
        var nivel = NivelPrecaucion.Normal;

        void Precaucion() { if (nivel == NivelPrecaucion.Normal) nivel = NivelPrecaucion.Precaucion; }

        if (cuestionario?.Seguridad is { AlgunaRespuestaAfirmativa: true })
        {
            nivel = NivelPrecaucion.ConsultarMedico;
            restricciones.Add(RestriccionSalud.SoloIntensidadBaja);
            restricciones.Add(RestriccionSalud.SinIntensidadAlta);
        }

        foreach (var condicion in cuestionario?.Condiciones?.Condiciones ?? [])
        {
            switch (condicion)
            {
                case CondicionSalud.Hipertension:
                case CondicionSalud.EnfermedadCardiaca:
                case CondicionSalud.EnfermedadRenal:
                case CondicionSalud.Diabetes:
                case CondicionSalud.Asma:
                    restricciones.Add(RestriccionSalud.SinIntensidadAlta);
                    Precaucion();
                    break;
                case CondicionSalud.Embarazo:
                case CondicionSalud.Posparto:
                    restricciones.Add(RestriccionSalud.SinIntensidadAlta);
                    restricciones.Add(RestriccionSalud.SinRestriccionCalorica);
                    Precaucion();
                    break;
                case CondicionSalud.HistorialTrastornoAlimentario:
                    restricciones.Add(RestriccionSalud.SinRestriccionCalorica);
                    Precaucion();
                    break;
                case CondicionSalud.Rodilla:
                    restricciones.Add(RestriccionSalud.SinImpacto);
                    restricciones.Add(RestriccionSalud.EvitarZonaRodilla);
                    Precaucion();
                    break;
                case CondicionSalud.EspaldaBaja:
                    restricciones.Add(RestriccionSalud.EvitarZonaEspaldaBaja);
                    Precaucion();
                    break;
                case CondicionSalud.Hombro:
                    restricciones.Add(RestriccionSalud.EvitarZonaHombro);
                    Precaucion();
                    break;
                case CondicionSalud.Osteoporosis:
                    restricciones.Add(RestriccionSalud.SinImpacto);
                    Precaucion();
                    break;
            }
        }

        if (perfil?.Edad is { } edad && edad < EdadAdulta)
        {
            // Menores de 18: sin pérdida de peso ni cargas altas.
            restricciones.Add(RestriccionSalud.SinRestriccionCalorica);
            restricciones.Add(RestriccionSalud.SinIntensidadAlta);
        }

        if (usarDatosDeSalud && perfil?.Imc is { } imc && imc < ImcBajoPeso)
        {
            restricciones.Add(RestriccionSalud.SinRestriccionCalorica);
            Precaucion();
        }

        var alergenos = (cuestionario?.Condiciones?.Alergias ?? [])
            .Where(a => a != Alergia.Ninguna)
            .Distinct()
            .OrderBy(a => a)
            .ToList();

        return new EvaluacionSalud(nivel, [.. restricciones], alergenos, Completitud(cuestionario));
    }

    /// <summary>
    /// Bloques que faltan para poder empezar un plan: el de seguridad siempre que el plan requiera cuestionario, el de
    /// condiciones si el plan tiene contraindicaciones o alérgenos (las alergias van en ese bloque) y el de objetivos si
    /// requiere cuestionario. Un plan que no requiere nada no pide ningún bloque (P1).
    /// </summary>
    public static IReadOnlyList<BloqueCuestionario> BloquesFaltantesPara(RequisitosSaludPlan requisitos, CuestionarioSalud? cuestionario)
    {
        ArgumentNullException.ThrowIfNull(requisitos);
        var completitud = Completitud(cuestionario);
        var faltan = new List<BloqueCuestionario>();

        if (requisitos.RequiereCuestionario && !completitud.Objetivos)
            faltan.Add(BloqueCuestionario.Objetivos);
        if (requisitos.RequiereCuestionario && !completitud.Seguridad)
            faltan.Add(BloqueCuestionario.Seguridad);
        if ((requisitos.TieneContraindicaciones || requisitos.TieneAlergenos) && !completitud.Condiciones)
            faltan.Add(BloqueCuestionario.Condiciones);

        return faltan;
    }

    private static CompletitudCuestionario Completitud(CuestionarioSalud? c) =>
        new(c?.Objetivos is not null, c?.Seguridad is not null, c?.Condiciones is not null);
}
