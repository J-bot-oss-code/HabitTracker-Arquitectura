using System.Text.RegularExpressions;

namespace HabitTracker.Domain.Perfil;

/// <summary>Jornada de la persona: es la puerta de entrada de las recomendaciones (P2).</summary>
public enum PerfilLaboral
{
    OficinaDiurna,
    TurnoNocturno,
    TurnosRotativos,
    Remoto,
    Estudiante,
    TrabajoFisico,
    SinEmpleo,
}

public enum Sexo
{
    Mujer,
    Hombre,
}

public enum NivelActividad
{
    Sedentaria,
    Ligera,
    Moderada,
    Alta,
    MuyAlta,
}

/// <summary>
/// Datos que la persona envía al guardar su perfil. El nivel 1 (perfil laboral, zona horaria y aviso de salud) es
/// obligatorio; el resto es opcional y se pide solo cuando un plan lo necesita (P1).
/// </summary>
public sealed record DatosPerfil(
    PerfilLaboral PerfilLaboral,
    string ZonaHoraria,
    bool AvisoSaludAceptado,
    TimeOnly? HoraEntradaTrabajo = null,
    TimeOnly? HoraSalidaTrabajo = null,
    TimeOnly? HoraDespertar = null,
    TimeOnly? HoraDormir = null,
    int? Edad = null,
    Sexo? Sexo = null,
    decimal? AlturaCm = null,
    decimal? PesoKg = null,
    NivelActividad? Actividad = null,
    int? DiasPorSemana = null,
    int? MinutosPorSesion = null,
    string? MetaSlug = null)
{
    /// <summary>Peso y altura son datos de salud: guardarlos exige el consentimiento <c>datosSalud</c>.</summary>
    public bool TraeDatosDeSalud => AlturaCm.HasValue || PesoKg.HasValue;
}

/// <summary>
/// Perfil de la persona en el módulo de negocio (una fila por usuario). Los límites son los del frontend
/// (<c>LIMITES_CORPORALES</c> y <c>LIMITES_PLAN</c>) y se validan aquí, en un solo punto (RD-07).
/// </summary>
public sealed partial class PerfilUsuario
{
    public const int EdadMinima = 16;
    public const int EdadMaxima = 100;
    public const decimal AlturaMinimaCm = 120;
    public const decimal AlturaMaximaCm = 230;
    public const decimal PesoMinimoKg = 30;
    public const decimal PesoMaximoKg = 300;

    public Guid UsuarioId { get; private set; }
    public PerfilLaboral PerfilLaboral { get; private set; }
    public string ZonaHoraria { get; private set; } = string.Empty;
    public bool AvisoSaludAceptado { get; private set; }
    public TimeOnly? HoraEntradaTrabajo { get; private set; }
    public TimeOnly? HoraSalidaTrabajo { get; private set; }
    public TimeOnly? HoraDespertar { get; private set; }
    public TimeOnly? HoraDormir { get; private set; }
    public int? Edad { get; private set; }
    public Sexo? Sexo { get; private set; }
    public decimal? AlturaCm { get; private set; }
    public decimal? PesoKg { get; private set; }
    public decimal? ObjetivoPesoKg { get; private set; }
    public NivelActividad? Actividad { get; private set; }
    public int? DiasPorSemana { get; private set; }
    public int? MinutosPorSesion { get; private set; }
    public string? MetaSlug { get; private set; }
    public DateTime? InicioPlan { get; private set; }
    public DateTime ActualizadoEn { get; private set; }

    private PerfilUsuario() { } // EF Core

    public static PerfilUsuario Crear(Guid usuarioId, DatosPerfil datos, DateTime ahoraUtc)
    {
        if (usuarioId == Guid.Empty)
            throw new UnauthorizedAccessException("Identidad de usuario inválida.");

        var perfil = new PerfilUsuario { UsuarioId = usuarioId };
        perfil.Actualizar(datos, ahoraUtc);
        return perfil;
    }

    /// <summary>Reemplaza los datos del perfil. El plan de interés reinicia <see cref="InicioPlan"/> solo si cambia.</summary>
    public void Actualizar(DatosPerfil datos, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(datos);
        ExigirUtc(ahoraUtc);
        Validar(datos);

        var slug = string.IsNullOrWhiteSpace(datos.MetaSlug) ? null : datos.MetaSlug.Trim();
        if (slug != MetaSlug)
            InicioPlan = slug is null ? null : ahoraUtc;

        PerfilLaboral = datos.PerfilLaboral;
        ZonaHoraria = datos.ZonaHoraria.Trim();
        AvisoSaludAceptado = datos.AvisoSaludAceptado;
        HoraEntradaTrabajo = datos.HoraEntradaTrabajo;
        HoraSalidaTrabajo = datos.HoraSalidaTrabajo;
        HoraDespertar = datos.HoraDespertar;
        HoraDormir = datos.HoraDormir;
        Edad = datos.Edad;
        Sexo = datos.Sexo;
        AlturaCm = datos.AlturaCm;
        PesoKg = datos.PesoKg;
        Actividad = datos.Actividad;
        DiasPorSemana = datos.DiasPorSemana;
        MinutosPorSesion = datos.MinutosPorSesion;
        MetaSlug = slug;
        ActualizadoEn = ahoraUtc;
    }

    /// <summary>El último peso registrado en Progreso pasa a ser el peso del perfil (antes podían divergir).</summary>
    public void SincronizarPeso(decimal pesoKg, DateTime ahoraUtc)
    {
        ValidarPeso(pesoKg, nameof(pesoKg));
        ExigirUtc(ahoraUtc);
        PesoKg = pesoKg;
        ActualizadoEn = ahoraUtc;
    }

    /// <summary>Fija o quita (con <c>null</c>) el peso objetivo de la gráfica de Progreso.</summary>
    public void FijarObjetivoPeso(decimal? objetivoKg, DateTime ahoraUtc)
    {
        if (objetivoKg.HasValue)
            ValidarPeso(objetivoKg.Value, nameof(objetivoKg));
        ExigirUtc(ahoraUtc);
        ObjetivoPesoKg = objetivoKg;
        ActualizadoEn = ahoraUtc;
    }

    /// <summary>Índice de masa corporal, si hay peso y altura. Lo usa el evaluador de salud, nunca la motivación (P4).</summary>
    public decimal? Imc => PesoKg is { } peso && AlturaCm is { } altura && altura > 0
        ? Math.Round(peso / (altura / 100m * (altura / 100m)), 1)
        : null;

    private static void Validar(DatosPerfil datos)
    {
        if (!Enum.IsDefined(datos.PerfilLaboral))
            throw new ArgumentException("El perfil laboral no es válido.", nameof(datos.PerfilLaboral));
        if (!EsZonaHorariaValida(datos.ZonaHoraria))
            throw new ArgumentException("La zona horaria no es válida.", nameof(datos.ZonaHoraria));
        if (!datos.AvisoSaludAceptado)
            throw new ArgumentException("Debes aceptar el aviso de salud.", nameof(datos.AvisoSaludAceptado));

        if (datos.Edad is { } edad && (edad < EdadMinima || edad > EdadMaxima))
            throw new ArgumentException($"La edad debe estar entre {EdadMinima} y {EdadMaxima}.", nameof(datos.Edad));
        if (datos.Sexo is { } sexo && !Enum.IsDefined(sexo))
            throw new ArgumentException("El sexo no es válido.", nameof(datos.Sexo));
        if (datos.AlturaCm is { } altura && (altura < AlturaMinimaCm || altura > AlturaMaximaCm))
            throw new ArgumentException($"La altura debe estar entre {AlturaMinimaCm} y {AlturaMaximaCm} cm.", nameof(datos.AlturaCm));
        if (datos.PesoKg is { } peso)
            ValidarPeso(peso, nameof(datos.PesoKg));
        if (datos.Actividad is { } actividad && !Enum.IsDefined(actividad))
            throw new ArgumentException("El nivel de actividad no es válido.", nameof(datos.Actividad));
        if (datos.DiasPorSemana is { } dias && (dias < 1 || dias > 7))
            throw new ArgumentException("Los días por semana deben estar entre 1 y 7.", nameof(datos.DiasPorSemana));
        if (datos.MinutosPorSesion is { } minutos && (minutos < 10 || minutos > 180))
            throw new ArgumentException("Los minutos por sesión deben estar entre 10 y 180.", nameof(datos.MinutosPorSesion));
        if (!string.IsNullOrWhiteSpace(datos.MetaSlug)
            && (datos.MetaSlug.Trim().Length > 60 || !SlugValido().IsMatch(datos.MetaSlug.Trim())))
            throw new ArgumentException("El plan de interés no es válido.", nameof(datos.MetaSlug));
    }

    private static void ValidarPeso(decimal pesoKg, string parametro)
    {
        if (pesoKg < PesoMinimoKg || pesoKg > PesoMaximoKg)
            throw new ArgumentException($"El peso debe estar entre {PesoMinimoKg} y {PesoMaximoKg} kg.", parametro);
    }

    /// <summary>Zona IANA (por ejemplo <c>America/Santo_Domingo</c>) que el sistema reconoce.</summary>
    public static bool EsZonaHorariaValida(string? zona)
    {
        if (string.IsNullOrWhiteSpace(zona) || zona.Length > 64)
            return false;
        return TimeZoneInfo.TryFindSystemTimeZoneById(zona.Trim(), out _);
    }

    private static void ExigirUtc(DateTime fecha)
    {
        if (fecha.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(fecha));
    }

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugValido();
}
