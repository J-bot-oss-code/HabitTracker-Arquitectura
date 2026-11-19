using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Salud;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Perfil de la persona y cuestionario de salud por bloques (Fase 2 de HabitTracker v2, RF-NEG-02). El nivel 1 (jornada,
/// zona horaria y aviso de salud) no pide datos de salud; el cuestionario, el peso y la altura exigen el consentimiento
/// <c>datosSalud</c> (409 <c>consentimiento_salud_requerido</c>). Solo traduce HTTP (RD-02).
/// </summary>
[ApiController]
[Route("api/profile")]
[Authorize(Roles = "Estandar")]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
public class ProfileController(IPerfilService perfiles, ICuestionarioSaludService cuestionarios) : ControllerBase
{
    /// <summary>Perfil de la persona. 404 <c>perfil_no_encontrado</c> si aún no hizo el onboarding.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken)
    {
        var perfil = await perfiles.ObtenerAsync(UsuarioId(), cancellationToken);
        return Ok(perfil with { Nombre = Nombre() });
    }

    /// <summary>Crea o reemplaza el perfil. Los campos del nivel 2 son opcionales.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(PerfilDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Guardar([FromBody] GuardarPerfilRequest request, CancellationToken cancellationToken)
    {
        var datos = new DatosPerfil(
            request.PerfilLaboral!.Value, request.ZonaHoraria, request.AvisoSaludAceptado,
            request.HoraEntradaTrabajo, request.HoraSalidaTrabajo, request.HoraDespertar, request.HoraDormir,
            request.Edad, request.Sexo, request.AlturaCm, request.PesoKg, request.Actividad,
            request.DiasPorSemana, request.MinutosPorSesion);
        var perfil = await perfiles.GuardarAsync(UsuarioId(), datos, cancellationToken);
        return Ok(perfil with { Nombre = Nombre() });
    }

    /// <summary>Bloques del cuestionario de salud y su evaluación (restricciones y avisos, nunca un diagnóstico).</summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(CuestionarioSaludDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerSalud(CancellationToken cancellationToken) =>
        Ok(await cuestionarios.ObtenerAsync(UsuarioId(), cancellationToken));

    /// <summary>Bloque (a): objetivos (hasta 3), zonas, experiencia y lugar.</summary>
    [HttpPut("health/objetivos")]
    [ProducesResponseType(typeof(CuestionarioSaludDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarObjetivos([FromBody] BloqueObjetivosRequest request, CancellationToken cancellationToken)
    {
        var bloque = new BloqueObjetivos(request.Objetivos!, request.Zonas ?? [], request.Experiencia!.Value, request.Lugar!.Value);
        return Ok(await cuestionarios.GuardarObjetivosAsync(UsuarioId(), bloque, cancellationToken));
    }

    /// <summary>Bloque (b): revisión de seguridad de 7 preguntas sí/no.</summary>
    [HttpPut("health/seguridad")]
    [ProducesResponseType(typeof(CuestionarioSaludDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarSeguridad([FromBody] BloqueSeguridadRequest request, CancellationToken cancellationToken)
    {
        var bloque = new BloqueSeguridad(
            request.AfeccionCardiacaOPresionAlta!.Value, request.DolorDePecho!.Value, request.MareosOPerdidaDeConocimiento!.Value,
            request.OtraEnfermedadCronica!.Value, request.MedicacionCronica!.Value, request.ProblemaOseoOArticular!.Value,
            request.SoloActividadSupervisada!.Value);
        return Ok(await cuestionarios.GuardarSeguridadAsync(UsuarioId(), bloque, cancellationToken));
    }

    /// <summary>Bloque (c): condiciones y alergias. "Ninguna" excluye a las demás.</summary>
    [HttpPut("health/condiciones")]
    [ProducesResponseType(typeof(CuestionarioSaludDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarCondiciones([FromBody] BloqueCondicionesRequest request, CancellationToken cancellationToken)
    {
        var bloque = new BloqueCondiciones(request.Condiciones!, request.Alergias!);
        return Ok(await cuestionarios.GuardarCondicionesAsync(UsuarioId(), bloque, cancellationToken));
    }

    private Guid UsuarioId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            throw new UnauthorizedAccessException("Sesión inválida.");
        return usuarioId;
    }

    // El nombre lo gestiona Control de Acceso y llega en el token (claim "name"), igual que en GET /api/auth/me.
    private string Nombre() => User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
}

public sealed record GuardarPerfilRequest(
    [Required] PerfilLaboral? PerfilLaboral,
    [Required, StringLength(64, MinimumLength = 1)] string ZonaHoraria,
    bool AvisoSaludAceptado,
    TimeOnly? HoraEntradaTrabajo = null,
    TimeOnly? HoraSalidaTrabajo = null,
    TimeOnly? HoraDespertar = null,
    TimeOnly? HoraDormir = null,
    [Range(PerfilUsuario.EdadMinima, PerfilUsuario.EdadMaxima)] int? Edad = null,
    Sexo? Sexo = null,
    [Range(typeof(decimal), "120", "230", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal? AlturaCm = null,
    [Range(typeof(decimal), "30", "300", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal? PesoKg = null,
    NivelActividad? Actividad = null,
    [Range(1, 7)] int? DiasPorSemana = null,
    [Range(10, 180)] int? MinutosPorSesion = null);

public sealed record BloqueObjetivosRequest(
    [Required, MinLength(1), MaxLength(BloqueObjetivos.MaximoObjetivos)] Objetivo[]? Objetivos,
    [MaxLength(4)] ZonaCorporal[]? Zonas,
    [Required] Experiencia? Experiencia,
    [Required] Lugar? Lugar);

public sealed record BloqueSeguridadRequest(
    [Required] bool? AfeccionCardiacaOPresionAlta,
    [Required] bool? DolorDePecho,
    [Required] bool? MareosOPerdidaDeConocimiento,
    [Required] bool? OtraEnfermedadCronica,
    [Required] bool? MedicacionCronica,
    [Required] bool? ProblemaOseoOArticular,
    [Required] bool? SoloActividadSupervisada);

public sealed record BloqueCondicionesRequest(
    [Required, MinLength(1), MaxLength(13)] CondicionSalud[]? Condiciones,
    [Required, MinLength(1), MaxLength(9)] Alergia[]? Alergias);
