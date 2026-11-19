using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Plan del día: usa el clima de una API externa (OpenWeatherMap) para recomendar dónde hacer los hábitos activos.
/// Solo traduce HTTP al servicio; las reglas están en el dominio (RecomendadorClima) y la llamada externa, en Infraestructura.
/// </summary>
[ApiController]
[Route("api/habits/plan-del-dia")]
[Authorize(Roles = "Estandar")]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
public class PlanDelDiaController : ControllerBase
{
    private readonly IPlanDelDiaService _service;

    public PlanDelDiaController(IPlanDelDiaService service)
    {
        _service = service;
    }

    /// <summary>Plan del día según el clima de la ciudad indicada.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PlanDelDiaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Obtener(
        [FromQuery, Required, StringLength(100, MinimumLength = 1)] string ciudad,
        CancellationToken cancellationToken)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var usuarioId))
        {
            throw new UnauthorizedAccessException("Sesión inválida.");
        }

        return Ok(await _service.ObtenerAsync(usuarioId, ciudad, cancellationToken));
    }
}
