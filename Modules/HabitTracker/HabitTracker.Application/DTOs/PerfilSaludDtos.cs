using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Progreso;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Application.DTOs;

/// <summary>
/// Perfil de la persona. <see cref="Nombre"/> lo pone la API desde el token (lo gestiona Control de Acceso). Los campos
/// del nivel 2 son opcionales (P1).
/// </summary>
public sealed record PerfilDto(
    string Nombre,
    PerfilLaboral PerfilLaboral,
    string ZonaHoraria,
    bool AvisoSaludAceptado,
    TimeOnly? HoraEntradaTrabajo,
    TimeOnly? HoraSalidaTrabajo,
    TimeOnly? HoraDespertar,
    TimeOnly? HoraDormir,
    int? Edad,
    Sexo? Sexo,
    decimal? AlturaCm,
    decimal? PesoKg,
    NivelActividad? Actividad,
    int? DiasPorSemana,
    int? MinutosPorSesion,
    string? MetaSlug,
    DateTime? InicioPlan,
    DateTime ActualizadoEn)
{
    public static PerfilDto Desde(PerfilUsuario p) => new(
        string.Empty, p.PerfilLaboral, p.ZonaHoraria, p.AvisoSaludAceptado, p.HoraEntradaTrabajo, p.HoraSalidaTrabajo,
        p.HoraDespertar, p.HoraDormir, p.Edad, p.Sexo, p.AlturaCm, p.PesoKg, p.Actividad, p.DiasPorSemana,
        p.MinutosPorSesion, p.MetaSlug, p.InicioPlan, p.ActualizadoEn);
}

/// <summary>
/// Cuestionario por bloques más la evaluación (restricciones y avisos, nunca un diagnóstico). Con el consentimiento
/// <c>datosSalud</c> retirado, las respuestas se siguen mostrando a su dueño pero la evaluación no las usa
/// (<see cref="UsaDatosDeSalud"/> en falso).
/// </summary>
public sealed record CuestionarioSaludDto(
    int Version,
    BloqueObjetivos? Objetivos,
    DateTime? ObjetivosRespondidoEn,
    BloqueSeguridad? Seguridad,
    DateTime? SeguridadRespondidoEn,
    BloqueCondiciones? Condiciones,
    DateTime? CondicionesRespondidoEn,
    EvaluacionSalud Evaluacion,
    bool UsaDatosDeSalud);

public sealed record PesoDto(Guid Id, DateTime Fecha, decimal PesoKg);

/// <summary>Contrato del frontend (<c>PesosDto</c>): el objetivo vive en el perfil.</summary>
public sealed record PesosDto(decimal? ObjetivoKg, IReadOnlyList<PesoDto> Registros);

/// <param name="CambiarObjetivo">Si es falso, el objetivo se conserva; si es verdadero, se fija <paramref name="ObjetivoKg"/> (null lo quita).</param>
public sealed record RegistrarPesoCommand(DateTime Fecha, decimal PesoKg, bool CambiarObjetivo = false, decimal? ObjetivoKg = null);

public sealed record MedicionDto(Guid Id, DateTime Fecha, ZonaMedicion Zona, decimal Valor);

public sealed record RegistrarMedicionCommand(DateTime Fecha, ZonaMedicion Zona, decimal Valor);

/// <summary>Sección "perfilYSalud" de la descarga de datos (derecho de acceso): el cuestionario va descifrado, es de su dueño.</summary>
public sealed record DatosPerfilSaludDto(
    PerfilDto? Perfil,
    CuestionarioSaludDto? Cuestionario,
    IReadOnlyList<PesoDto> Pesos,
    IReadOnlyList<MedicionDto> Mediciones);
