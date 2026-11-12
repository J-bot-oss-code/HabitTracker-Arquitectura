using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Planes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Los planes que la persona sigue (Fase 4 de HabitTracker v2). La identidad sale siempre del token y un plan ajeno responde
/// 404 como uno que no existe (RD-06). La lógica vive en <see cref="IPlanUsuarioService"/> y en el dominio (RD-02).
/// </summary>
[ApiController]
[Route("api/my-plans")]
[Authorize(Roles = "Estandar")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class MyPlansController(IPlanUsuarioService planes) : ControllerBase
{
    /// <summary>
    /// Empieza un plan del catálogo. <c>400 plan_no_apto</c> si no es para la persona, <c>409 consentimiento_salud_requerido</c>
    /// solo si el plan pide el cuestionario, <c>400 limite_planes</c> con 3 planes en curso y <c>400 plan_ya_activo</c>.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PlanUsuarioDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Inscribirse([FromBody] InscribirseRequest request, CancellationToken cancellationToken)
    {
        var plan = await planes.InscribirseAsync(UsuarioId(), new InscribirseCommand(request.Slug, request.Ancla, request.Hora), cancellationToken);
        return Created($"/api/my-plans/{plan.Id}", plan);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlanUsuarioDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        Ok(await planes.ListarAsync(UsuarioId(), cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PlanUsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken) =>
        Ok(await planes.ObtenerAsync(UsuarioId(), id, cancellationToken));

    /// <summary>Pausar (con motivo), reanudar, completar (solo al llegar al final) o cancelar. Los hábitos del plan acompañan.</summary>
    [HttpPost("{id:guid}/transition")]
    [ProducesResponseType(typeof(PlanUsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoPlanRequest request, CancellationToken cancellationToken) =>
        Ok(await planes.CambiarEstadoAsync(
            UsuarioId(), id,
            new CambiarEstadoPlanCommand(request.NuevoEstado!.Value, request.MotivoPausa, request.ConservarHabitos),
            cancellationToken));

    /// <summary>Un ajuste que la persona eligió: modo suave (7 días) o extender una semana (máximo 2 veces).</summary>
    [HttpPost("{id:guid}/adjust")]
    [ProducesResponseType(typeof(PlanUsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Ajustar(Guid id, [FromBody] AjustarPlanRequest request, CancellationToken cancellationToken) =>
        Ok(await planes.AjustarAsync(UsuarioId(), id, new AjustarPlanCommand(request.Tipo!.Value), cancellationToken));

    [HttpPut("{id:guid}/reminder")]
    [ProducesResponseType(typeof(PlanUsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Recordatorio(Guid id, [FromBody] RecordatorioPlanRequest request, CancellationToken cancellationToken) =>
        Ok(await planes.ConfigurarRecordatorioAsync(
            UsuarioId(), id, new ConfigurarRecordatorioCommand(request.Hora, request.RecordatoriosActivos), cancellationToken));

    private Guid UsuarioId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            throw new UnauthorizedAccessException("Sesión inválida.");
        return usuarioId;
    }
}

public record InscribirseRequest(
    [Required, StringLength(PlanUsuario.LongitudSlug, MinimumLength = 1)] string Slug,
    Ancla? Ancla = null,
    TimeOnly? Hora = null);

public record CambiarEstadoPlanRequest(
    [Required] EstadoPlanUsuario? NuevoEstado,
    MotivoPausa? MotivoPausa = null,
    bool ConservarHabitos = false);

public record AjustarPlanRequest([Required] TipoAjuste? Tipo);

public record RecordatorioPlanRequest(TimeOnly? Hora, bool RecordatoriosActivos);
