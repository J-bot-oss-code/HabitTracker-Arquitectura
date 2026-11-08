namespace HabitTracker.Domain.Salud;

public enum BloqueCuestionario
{
    Objetivos,
    Seguridad,
    Condiciones,
}

public enum Objetivo
{
    PerderPeso,
    GanarMasaMuscular,
    DefinirTonificar,
    ReducirEstres,
    MejorarConcentracion,
    DormirMejor,
    Hidratarme,
    MasEnergia,
    PosturaYMovilidad,
}

/// <summary>Zona a definir o tonificar (solo con el objetivo <see cref="Objetivo.DefinirTonificar"/>).</summary>
public enum ZonaCorporal
{
    AbdomenYCore,
    GluteosYPiernas,
    BrazosYHombros,
    Espalda,
}

public enum Experiencia
{
    Ninguna,
    Principiante,
    Intermedio,
}

public enum Lugar
{
    CasaSinEquipo,
    CasaConEquipoBasico,
    Gimnasio,
}

public enum CondicionSalud
{
    Ninguna,
    Hipertension,
    Diabetes,
    Asma,
    EnfermedadCardiaca,
    EnfermedadRenal,
    Rodilla,
    EspaldaBaja,
    Hombro,
    Osteoporosis,
    Embarazo,
    Posparto,
    HistorialTrastornoAlimentario,
}

public enum Alergia
{
    Ninguna,
    Lactosa,
    Gluten,
    Mani,
    FrutosSecos,
    Mariscos,
    Pescado,
    Huevo,
    Soya,
}

/// <summary>Bloque (a): objetivos (hasta 3), experiencia y lugar.</summary>
public sealed record BloqueObjetivos(
    IReadOnlyList<Objetivo> Objetivos,
    IReadOnlyList<ZonaCorporal> Zonas,
    Experiencia Experiencia,
    Lugar Lugar)
{
    public const int MaximoObjetivos = 3;

    public void Validar()
    {
        if (Objetivos is null || Objetivos.Count == 0 || Objetivos.Count > MaximoObjetivos)
            throw new ArgumentException($"Elige entre 1 y {MaximoObjetivos} objetivos.", nameof(Objetivos));
        CuestionarioSalud.ExigirDefinidosYSinRepetir(Objetivos, nameof(Objetivos));
        CuestionarioSalud.ExigirDefinidosYSinRepetir(Zonas ?? [], nameof(Zonas));
        if ((Zonas?.Count ?? 0) > 0 && !Objetivos.Contains(Objetivo.DefinirTonificar))
            throw new ArgumentException("Las zonas solo aplican al objetivo de definir o tonificar.", nameof(Zonas));
        if (!Enum.IsDefined(Experiencia))
            throw new ArgumentException("La experiencia no es válida.", nameof(Experiencia));
        if (!Enum.IsDefined(Lugar))
            throw new ArgumentException("El lugar no es válido.", nameof(Lugar));
    }
}

/// <summary>
/// Bloque (b): revisión de seguridad de 7 preguntas sí/no inspiradas en el PAR-Q+ (redacción propia). Cualquier "sí"
/// lleva a "consulta a tu médico" y limita los planes a intensidad baja.
/// </summary>
public sealed record BloqueSeguridad(
    bool AfeccionCardiacaOPresionAlta,
    bool DolorDePecho,
    bool MareosOPerdidaDeConocimiento,
    bool OtraEnfermedadCronica,
    bool MedicacionCronica,
    bool ProblemaOseoOArticular,
    bool SoloActividadSupervisada)
{
    public bool AlgunaRespuestaAfirmativa =>
        AfeccionCardiacaOPresionAlta || DolorDePecho || MareosOPerdidaDeConocimiento || OtraEnfermedadCronica
        || MedicacionCronica || ProblemaOseoOArticular || SoloActividadSupervisada;
}

/// <summary>Bloque (c): condiciones y alergias, listas cerradas sin texto libre. "Ninguna" excluye a las demás.</summary>
public sealed record BloqueCondiciones(
    IReadOnlyList<CondicionSalud> Condiciones,
    IReadOnlyList<Alergia> Alergias)
{
    public void Validar()
    {
        ExigirListaConNinguna(Condiciones, CondicionSalud.Ninguna, nameof(Condiciones));
        ExigirListaConNinguna(Alergias, Alergia.Ninguna, nameof(Alergias));
    }

    private static void ExigirListaConNinguna<T>(IReadOnlyList<T> valores, T ninguna, string campo) where T : struct, Enum
    {
        if (valores is null || valores.Count == 0)
            throw new ArgumentException("Marca al menos una opción (o \"Ninguna\").", campo);
        CuestionarioSalud.ExigirDefinidosYSinRepetir(valores, campo);
        if (valores.Contains(ninguna) && valores.Count > 1)
            throw new ArgumentException("\"Ninguna\" no se puede combinar con otras opciones.", campo);
    }
}

/// <summary>
/// Cuestionario de salud por bloques (una fila por usuario). Cada bloque se guarda por separado con su fecha, y un plan
/// exige solo los bloques que necesita (<see cref="EvaluadorSalud.BloquesFaltantesPara"/>). Es un dato de salud: la
/// infraestructura lo guarda cifrado y solo lo lee su dueño.
/// </summary>
public sealed class CuestionarioSalud
{
    /// <summary>Versión de las preguntas: si cambian, las respuestas viejas se pueden identificar.</summary>
    public const int VersionActual = 1;

    public Guid UsuarioId { get; private set; }
    public int Version { get; private set; } = VersionActual;
    public BloqueObjetivos? Objetivos { get; private set; }
    public DateTime? ObjetivosRespondidoEn { get; private set; }
    public BloqueSeguridad? Seguridad { get; private set; }
    public DateTime? SeguridadRespondidoEn { get; private set; }
    public BloqueCondiciones? Condiciones { get; private set; }
    public DateTime? CondicionesRespondidoEn { get; private set; }

    public CuestionarioSalud(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
            throw new UnauthorizedAccessException("Identidad de usuario inválida.");
        UsuarioId = usuarioId;
    }

    /// <summary>Reconstruye el cuestionario guardado (lo usa el repositorio tras descifrarlo).</summary>
    public static CuestionarioSalud Reconstruir(
        Guid usuarioId, int version,
        BloqueObjetivos? objetivos, DateTime? objetivosEn,
        BloqueSeguridad? seguridad, DateTime? seguridadEn,
        BloqueCondiciones? condiciones, DateTime? condicionesEn) =>
        new(usuarioId)
        {
            Version = version,
            Objetivos = objetivos,
            ObjetivosRespondidoEn = objetivosEn,
            Seguridad = seguridad,
            SeguridadRespondidoEn = seguridadEn,
            Condiciones = condiciones,
            CondicionesRespondidoEn = condicionesEn,
        };

    public void GuardarObjetivos(BloqueObjetivos bloque, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(bloque);
        bloque.Validar();
        Objetivos = bloque with { Objetivos = [.. bloque.Objetivos], Zonas = [.. bloque.Zonas ?? []] };
        ObjetivosRespondidoEn = Utc(ahoraUtc);
        Version = VersionActual;
    }

    public void GuardarSeguridad(BloqueSeguridad bloque, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(bloque);
        Seguridad = bloque;
        SeguridadRespondidoEn = Utc(ahoraUtc);
        Version = VersionActual;
    }

    public void GuardarCondiciones(BloqueCondiciones bloque, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(bloque);
        bloque.Validar();
        Condiciones = bloque with { Condiciones = [.. bloque.Condiciones], Alergias = [.. bloque.Alergias] };
        CondicionesRespondidoEn = Utc(ahoraUtc);
        Version = VersionActual;
    }

    internal static void ExigirDefinidosYSinRepetir<T>(IReadOnlyList<T> valores, string campo) where T : struct, Enum
    {
        if (valores.Any(v => !Enum.IsDefined(v)))
            throw new ArgumentException("Hay una opción que no es válida.", campo);
        if (valores.Distinct().Count() != valores.Count)
            throw new ArgumentException("Hay opciones repetidas.", campo);
    }

    private static DateTime Utc(DateTime fecha) => fecha.Kind == DateTimeKind.Utc
        ? fecha
        : throw new ArgumentException("La fecha debe ser UTC.", nameof(fecha));
}
