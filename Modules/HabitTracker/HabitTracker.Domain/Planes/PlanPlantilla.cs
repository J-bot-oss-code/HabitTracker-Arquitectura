using System.Text.RegularExpressions;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Domain.Planes;

public enum AreaPlan
{
    ControlDePeso,
    Gimnasio,
    Estres,
    Concentracion,
    Descanso,
    Hidratacion,
    Nutricion,
    Movilidad,
    Energia,
}

/// <summary>Básico: de 1 a 3 hábitos simples durante 2 a 4 semanas. Regular: un programa con guía semanal.</summary>
public enum NivelPlan
{
    Basico,
    Regular,
}

public enum IntensidadPlan
{
    Baja,
    Media,
    Alta,
}

public enum SexoPlan
{
    Todos,
    Mujer,
    Hombre,
}

/// <summary>Momento de la jornada al que se ata una tarea. La hora concreta la calcula el ResolvedorAncla (Fase 4).</summary>
public enum Ancla
{
    AlDespertar,
    AlLlegarAlTrabajo,
    AMediaManana,
    DespuesDelAlmuerzo,
    AMediaTarde,
    AlSalirDelTrabajo,
    AntesDeCenar,
    AntesDeDormir,
}

/// <summary>Qué cambia en una semana del plan. El <see cref="TipExtra"/> se desbloquea al cerrar bien la semana (Fase 4).</summary>
public sealed record SemanaGuia(int Semana, string Texto, string? TipExtra);

/// <summary>Una tarea del plan. Al inscribirse (Fase 4) se copia a un hábito; la meta es fija y la progresión va en la guía.</summary>
public sealed class TareaPlantilla
{
    public const int LongitudNombre = 200;
    public const int LongitudInstrucciones = 1000;
    public const int LongitudUnidad = 50;
    public const int LongitudVersionMinima = 200;
    public const decimal CantidadMaxima = 1000m;

    /// <summary>Unidades de las tareas de agua: con ellas se aplican los topes de hidratación prudente (calor, P7).</summary>
    public const string UnidadVasos = "vasos";
    public const string UnidadLitros = "litros";
    public const decimal MaximoVasosAlDia = 20m;
    public const decimal MaximoLitrosAlDia = 5m;

    private TareaPlantilla() { } // EF Core

    public TareaPlantilla(int orden, string nombre, string instrucciones, Periodicidad periodicidad, decimal cantidad,
        string unidad, string versionMinima, Ancla anclaSugerida)
    {
        if (orden < 1 || orden > PlanPlantilla.MaximoTareasRegular)
            throw new ArgumentException("El orden de la tarea no es válido.", nameof(orden));
        if (!Enum.IsDefined(periodicidad))
            throw new ArgumentException("La periodicidad no es válida.", nameof(periodicidad));
        if (!Enum.IsDefined(anclaSugerida))
            throw new ArgumentException("El ancla no es válida.", nameof(anclaSugerida));
        if (cantidad <= 0 || cantidad > CantidadMaxima)
            throw new ArgumentException("La cantidad de la tarea está fuera de rango.", nameof(cantidad));

        Id = Guid.NewGuid();
        Orden = orden;
        Nombre = PlanPlantilla.Texto(nombre, LongitudNombre, nameof(nombre));
        Instrucciones = PlanPlantilla.Texto(instrucciones, LongitudInstrucciones, nameof(instrucciones));
        Periodicidad = periodicidad;
        Cantidad = cantidad;
        Unidad = PlanPlantilla.Texto(unidad, LongitudUnidad, nameof(unidad));
        VersionMinima = PlanPlantilla.Texto(versionMinima, LongitudVersionMinima, nameof(versionMinima));
        AnclaSugerida = anclaSugerida;

        if (EsDeAgua && Periodicidad == Periodicidad.Diaria)
        {
            var tope = Unidad == UnidadVasos ? MaximoVasosAlDia : MaximoLitrosAlDia;
            if (Cantidad > tope)
                throw new ArgumentException($"Una tarea de agua no puede pasar de {tope} {Unidad} al día.", nameof(cantidad));
        }
    }

    public Guid Id { get; private set; }
    public int Orden { get; private set; }
    public string Nombre { get; private set; } = string.Empty;
    public string Instrucciones { get; private set; } = string.Empty;
    public Periodicidad Periodicidad { get; private set; }
    public decimal Cantidad { get; private set; }
    public string Unidad { get; private set; } = string.Empty;
    /// <summary>"Hoy solo lo mínimo": la versión que cuenta en un mal día (P3).</summary>
    public string VersionMinima { get; private set; } = string.Empty;
    public Ancla AnclaSugerida { get; private set; }

    public bool EsDeAgua => Unidad is UnidadVasos or UnidadLitros;
}

/// <summary>Todo el contenido editable de un plan, tal como viene del catálogo. Lo valida <see cref="PlanPlantilla"/>.</summary>
public sealed record ContenidoPlan(
    string Slug,
    string Nombre,
    string Resumen,
    string Descripcion,
    AreaPlan Area,
    NivelPlan Nivel,
    IntensidadPlan Intensidad,
    SexoPlan Sexo,
    int EdadMinima,
    int EdadMaxima,
    int Semanas,
    IReadOnlyList<PerfilLaboral> PerfilesLaborales,
    Lugar? Lugar,
    bool Impacto,
    IReadOnlyList<ZonaCorporal> ZonasCorporales,
    IReadOnlyList<CondicionSalud> Contraindicaciones,
    IReadOnlyList<Alergia> Alergenos,
    IReadOnlyList<string> Beneficios,
    string AvisoSeguridad,
    IReadOnlyList<SemanaGuia> GuiaSemanal,
    bool RequiereCuestionario,
    int VersionContenido,
    IReadOnlyList<TareaPlantilla> Tareas);

/// <summary>
/// Plan del catálogo (Fase 3 de HabitTracker v2). El slug es estable y un plan retirado se desactiva, nunca se borra,
/// porque puede haber personas siguiéndolo. Las invariantes de seguridad del contenido viven aquí, así que un error en
/// <c>catalogo-planes.json</c> rompe el arranque y la prueba de contenido en lugar de llegar a la persona.
/// </summary>
public sealed partial class PlanPlantilla
{
    public const int LongitudSlug = 60;
    public const int LongitudNombre = 100;
    public const int LongitudResumen = 200;
    public const int LongitudDescripcion = 1000;
    public const int LongitudAviso = 500;
    public const int LongitudBeneficio = 150;
    public const int LongitudGuia = 300;
    public const int EdadMinimaPermitida = 16; // la de los términos de uso
    public const int EdadMaximaPermitida = 100;
    public const int NumeroBeneficios = 3;
    public const int MaximoTareasBasico = 3;
    public const int MaximoTareasRegular = 8;
    public const int SemanasMinimasBasico = 2;
    public const int SemanasMaximasBasico = 4;
    // "Reducir el estrés" dura 21 días y es un programa con guía semanal: por eso un Regular empieza en 3 semanas.
    public const int SemanasMinimasRegular = 3;
    public const int SemanasMaximasRegular = 12;

    private List<TareaPlantilla> _tareas = [];

    private PlanPlantilla() { } // EF Core

    public PlanPlantilla(ContenidoPlan contenido)
    {
        Id = Guid.NewGuid();
        Activo = true;
        Aplicar(contenido);
    }

    public Guid Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string Resumen { get; private set; } = string.Empty;
    public string Descripcion { get; private set; } = string.Empty;
    public AreaPlan Area { get; private set; }
    public NivelPlan Nivel { get; private set; }
    public IntensidadPlan Intensidad { get; private set; }
    public SexoPlan Sexo { get; private set; }
    public int EdadMinima { get; private set; }
    public int EdadMaxima { get; private set; }
    public int Semanas { get; private set; }
    /// <summary>Jornadas para las que está pensado. Vacío: para todas.</summary>
    public List<PerfilLaboral> PerfilesLaborales { get; private set; } = [];
    /// <summary>Lugar que necesita. Nulo: sirve en cualquier lugar.</summary>
    public Lugar? Lugar { get; private set; }
    public bool Impacto { get; private set; }
    public List<ZonaCorporal> ZonasCorporales { get; private set; } = [];
    public List<CondicionSalud> Contraindicaciones { get; private set; } = [];
    public List<Alergia> Alergenos { get; private set; } = [];
    public List<string> Beneficios { get; private set; } = [];
    public string AvisoSeguridad { get; private set; } = string.Empty;
    public List<SemanaGuia> GuiaSemanal { get; private set; } = [];
    /// <summary>Si es verdadero, empezar el plan exige el cuestionario de salud y el consentimiento (Fase 4).</summary>
    public bool RequiereCuestionario { get; private set; }
    public int VersionContenido { get; private set; }
    public bool Activo { get; private set; }
    /// <summary>Siempre por <see cref="TareaPlantilla.Orden"/>: la base no garantiza el orden al leerlas.</summary>
    public IReadOnlyList<TareaPlantilla> Tareas => [.. _tareas.OrderBy(t => t.Orden)];

    public bool IncluyeAgua => _tareas.Exists(t => t.EsDeAgua);

    public RequisitosSaludPlan RequisitosSalud => new(RequiereCuestionario, Alergenos.Count > 0, Contraindicaciones.Count > 0);

    /// <summary>
    /// Si el plan usa algún dato que solo se pide en el nivel 2 (área de peso o gimnasio, intensidad media o alta,
    /// contraindicaciones, alérgenos, o un rango de edad o un sexo concretos), debe exigir el cuestionario.
    /// </summary>
    public static bool NecesitaCuestionario(ContenidoPlan c)
    {
        ArgumentNullException.ThrowIfNull(c);
        return c.Area is AreaPlan.ControlDePeso or AreaPlan.Gimnasio
            || c.Intensidad != IntensidadPlan.Baja
            || c.Contraindicaciones.Count > 0
            || c.Alergenos.Count > 0
            || c.EdadMinima > EdadMinimaPermitida
            || c.EdadMaxima < EdadMaximaPermitida
            || c.Sexo != SexoPlan.Todos;
    }

    /// <summary>Reemplaza el contenido por una versión más nueva del catálogo. El slug no cambia nunca.</summary>
    public void Actualizar(ContenidoPlan contenido)
    {
        ArgumentNullException.ThrowIfNull(contenido);
        if (!string.Equals(contenido.Slug, Slug, StringComparison.Ordinal))
            throw new InvalidOperationException("El slug de un plan no se puede cambiar.");
        Aplicar(contenido);
        Activo = true;
    }

    /// <summary>El plan sale del catálogo, pero quien ya lo sigue lo conserva.</summary>
    public void Retirar() => Activo = false;

    private void Aplicar(ContenidoPlan c)
    {
        ArgumentNullException.ThrowIfNull(c);

        if (string.IsNullOrWhiteSpace(c.Slug) || c.Slug.Length > LongitudSlug || !FormatoSlug().IsMatch(c.Slug))
            throw new ArgumentException("El slug no es válido.", nameof(c.Slug));
        Definido(c.Area, nameof(c.Area));
        Definido(c.Nivel, nameof(c.Nivel));
        Definido(c.Intensidad, nameof(c.Intensidad));
        Definido(c.Sexo, nameof(c.Sexo));
        if (c.Lugar is { } lugar)
            Definido(lugar, nameof(c.Lugar));
        if (c.EdadMinima < EdadMinimaPermitida || c.EdadMaxima > EdadMaximaPermitida || c.EdadMinima > c.EdadMaxima)
            throw new ArgumentException("El rango de edad no es válido.", nameof(c.EdadMinima));
        if (c.VersionContenido < 1)
            throw new ArgumentException("La versión del contenido debe ser 1 o mayor.", nameof(c.VersionContenido));

        var (minSemanas, maxSemanas, maxTareas) = c.Nivel == NivelPlan.Basico
            ? (SemanasMinimasBasico, SemanasMaximasBasico, MaximoTareasBasico)
            : (SemanasMinimasRegular, SemanasMaximasRegular, MaximoTareasRegular);
        if (c.Semanas < minSemanas || c.Semanas > maxSemanas)
            throw new ArgumentException($"Un plan {c.Nivel} dura de {minSemanas} a {maxSemanas} semanas.", nameof(c.Semanas));

        if (c.Nivel == NivelPlan.Basico && (c.Area == AreaPlan.ControlDePeso || c.Intensidad == IntensidadPlan.Alta))
            throw new ArgumentException("Un plan Básico no puede ser de control de peso ni de intensidad alta.", nameof(c.Nivel));
        if (NecesitaCuestionario(c) && !c.RequiereCuestionario)
            throw new ArgumentException("Este plan usa datos de salud o de nivel 2 y debe exigir el cuestionario.", nameof(c.RequiereCuestionario));

        var tareas = c.Tareas ?? throw new ArgumentException("Faltan las tareas.", nameof(c.Tareas));
        if (tareas.Count == 0 || tareas.Count > maxTareas)
            throw new ArgumentException($"Un plan {c.Nivel} tiene de 1 a {maxTareas} tareas.", nameof(c.Tareas));
        if (tareas.Any(t => t is null) || tareas.Select(t => t.Orden).Distinct().Count() != tareas.Count)
            throw new ArgumentException("Las tareas deben tener un orden distinto.", nameof(c.Tareas));

        var beneficios = (c.Beneficios ?? []).Select(b => Texto(b, LongitudBeneficio, nameof(c.Beneficios))).ToList();
        if (beneficios.Count != NumeroBeneficios)
            throw new ArgumentException($"Un plan tiene exactamente {NumeroBeneficios} beneficios.", nameof(c.Beneficios));

        var guia = ValidarGuia(c);

        Slug = c.Slug;
        Nombre = Texto(c.Nombre, LongitudNombre, nameof(c.Nombre));
        Resumen = Texto(c.Resumen, LongitudResumen, nameof(c.Resumen));
        Descripcion = Texto(c.Descripcion, LongitudDescripcion, nameof(c.Descripcion));
        AvisoSeguridad = Texto(c.AvisoSeguridad, LongitudAviso, nameof(c.AvisoSeguridad));
        Area = c.Area;
        Nivel = c.Nivel;
        Intensidad = c.Intensidad;
        Sexo = c.Sexo;
        EdadMinima = c.EdadMinima;
        EdadMaxima = c.EdadMaxima;
        Semanas = c.Semanas;
        PerfilesLaborales = SinRepetir(c.PerfilesLaborales, nameof(c.PerfilesLaborales));
        Lugar = c.Lugar;
        Impacto = c.Impacto;
        ZonasCorporales = SinRepetir(c.ZonasCorporales, nameof(c.ZonasCorporales));
        Contraindicaciones = SinRepetir(c.Contraindicaciones, nameof(c.Contraindicaciones));
        if (Contraindicaciones.Contains(CondicionSalud.Ninguna))
            throw new ArgumentException("\"Ninguna\" no es una contraindicación.", nameof(c.Contraindicaciones));
        Alergenos = SinRepetir(c.Alergenos, nameof(c.Alergenos));
        if (Alergenos.Contains(Alergia.Ninguna))
            throw new ArgumentException("\"Ninguna\" no es un alérgeno.", nameof(c.Alergenos));
        Beneficios = beneficios;
        GuiaSemanal = guia;
        RequiereCuestionario = c.RequiereCuestionario;
        VersionContenido = c.VersionContenido;
        _tareas = [.. tareas.OrderBy(t => t.Orden)];
    }

    private static List<SemanaGuia> ValidarGuia(ContenidoPlan c)
    {
        var guia = (c.GuiaSemanal ?? []).ToList();
        // Un Regular es un programa: cada semana dice qué cambia. En un Básico la guía es opcional.
        if (c.Nivel == NivelPlan.Regular && guia.Count != c.Semanas)
            throw new ArgumentException("Un plan Regular necesita una guía para cada semana.", nameof(c.GuiaSemanal));
        if (guia.Count != 0 && guia.Count != c.Semanas)
            throw new ArgumentException("La guía tiene una entrada por semana o ninguna.", nameof(c.GuiaSemanal));

        for (var i = 0; i < guia.Count; i++)
        {
            var semana = guia[i] ?? throw new ArgumentException("Falta una semana de la guía.", nameof(c.GuiaSemanal));
            if (semana.Semana != i + 1)
                throw new ArgumentException("Las semanas de la guía van en orden desde la 1.", nameof(c.GuiaSemanal));
            var tip = semana.TipExtra is null ? null : Texto(semana.TipExtra, LongitudGuia, nameof(c.GuiaSemanal));
            guia[i] = new SemanaGuia(semana.Semana, Texto(semana.Texto, LongitudGuia, nameof(c.GuiaSemanal)), tip);
        }

        return guia;
    }

    internal static string Texto(string? valor, int maximo, string campo)
    {
        var texto = valor?.Trim();
        if (string.IsNullOrEmpty(texto) || texto.Length > maximo)
            throw new ArgumentException($"El campo {campo} es obligatorio y tiene {maximo} caracteres como máximo.", campo);
        return texto;
    }

    private static void Definido<T>(T valor, string campo) where T : struct, Enum
    {
        if (!Enum.IsDefined(valor))
            throw new ArgumentException($"El valor de {campo} no es válido.", campo);
    }

    private static List<T> SinRepetir<T>(IReadOnlyList<T>? valores, string campo) where T : struct, Enum
    {
        var lista = (valores ?? []).ToList();
        foreach (var valor in lista)
            Definido(valor, campo);
        if (lista.Distinct().Count() != lista.Count)
            throw new ArgumentException($"El campo {campo} tiene valores repetidos.", campo);
        return [.. lista.OrderBy(v => v)];
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex FormatoSlug();
}
