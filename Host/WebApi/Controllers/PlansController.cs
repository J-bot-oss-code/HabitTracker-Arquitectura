using System.Security.Claims;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// Catálogo de planes (Fase 3 de HabitTracker v2). El catálogo y el detalle son públicos, para la landing, con caché
/// pública de 5 minutos y límite por IP. Los recomendados son de cada persona y usan su perfil y, con su consentimiento,
/// su cuestionario de salud (RD-06).
/// </summary>
[ApiController]
[Route("api/plans")]
public class PlansController(IPlanesService planes) : ControllerBase
{
    private const int SegundosDeCache = 300;

    /// <summary>Planes activos. Filtros opcionales: <c>area</c>, <c>nivel</c> y <c>perfilLaboral</c>.</summary>
    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Catalogo)]
    [ResponseCache(Duration = SegundosDeCache, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(typeof(IReadOnlyList<PlanResumenDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] AreaPlan? area, [FromQuery] NivelPlan? nivel, [FromQuery] PerfilLaboral? perfilLaboral,
        CancellationToken cancellationToken) =>
        Ok(await planes.ListarAsync(new FiltrosPlanes(area, nivel, perfilLaboral), cancellationToken));

    /// <summary>Recomendados para la persona con sus motivos, más los no aptos con su razón.</summary>
    [HttpGet("recommended")]
    [Authorize(Roles = "Estandar")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(RecomendacionesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Recomendados(CancellationToken cancellationToken) =>
        Ok(await planes.RecomendarAsync(UsuarioId(), cancellationToken));

    /// <summary>Detalle de un plan: tareas, guía semanal y avisos de seguridad. 404 <c>plan_no_encontrado</c>.</summary>
    [HttpGet("{slug}")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Catalogo)]
    [ResponseCache(Duration = SegundosDeCache, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(typeof(PlanDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(string slug, CancellationToken cancellationToken) =>
        Ok(await planes.ObtenerAsync(slug, cancellationToken));

    private Guid UsuarioId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            throw new UnauthorizedAccessException("Sesión inválida.");
        return usuarioId;
    }
}
