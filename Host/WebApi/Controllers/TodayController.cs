using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;
using System.Security.Claims;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Hoy y el resumen de constancia (Fase 4). <c>GET /api/today</c> es un endpoint nuevo y no una extensión de
/// <c>plan-del-dia</c>: ese depende de la API del clima y responde 503 cuando cae, y el check-in diario no puede fallar por eso.
/// </summary>
[ApiController]
[Authorize(Roles = "Estandar")]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class TodayController(IHoyService hoy, IResumenProgresoService resumen) : ControllerBase
{
    /// <summary>Las tareas de hoy de los planes activos, el mensaje del día y la celebración de la semana, si hay.</summary>
    [HttpGet("api/today")]
    [ProducesResponseType(typeof(HoyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Hoy(CancellationToken cancellationToken) =>
        Ok(await hoy.ObtenerHoyAsync(UsuarioId(), cancellationToken));

    /// <summary>Racha, récord, logros, «tu mes en números» y el mensaje del día. Nada de esto usa peso ni medidas (P4).</summary>
    [HttpGet("api/progress/summary")]
    [ProducesResponseType(typeof(ResumenProgresoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resumen(CancellationToken cancellationToken) =>
        Ok(await resumen.ObtenerResumenAsync(UsuarioId(), cancellationToken));

    /// <summary>Marca los logros como celebrados: cada uno se felicita una sola vez.</summary>
    [HttpPost("api/progress/achievements/seen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> LogrosVistos(CancellationToken cancellationToken)
    {
        await resumen.MarcarLogrosVistosAsync(UsuarioId(), cancellationToken);
        return NoContent();
    }

    private Guid UsuarioId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            throw new UnauthorizedAccessException("Sesión inválida.");
        return usuarioId;
    }
}
