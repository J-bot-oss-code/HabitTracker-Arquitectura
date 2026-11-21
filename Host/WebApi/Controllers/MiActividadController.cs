using System.Security.Claims;
using Auditing.Application.DTOs;
using Auditing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// «Mi actividad»: lo que la propia persona hizo en su cuenta (rol Estándar o Administrador), leído del historial de auditoría.
/// La persona sale del token, nunca de la URL: no hay forma de pedir la actividad de otra (RD-06). Solo lectura (RF-AUD-06); la
/// lógica vive en <see cref="IRegistroAuditoriaService"/> (RD-02).
/// </summary>
[ApiController]
[Route("api/me/activity")]
[Authorize]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class MiActividadController(IRegistroAuditoriaService auditoria) : ControllerBase
{
    /// <summary>De lo más reciente a lo más antiguo, en los últimos <c>dias</c> (7 o 30) y paginado.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaAuditoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Consultar(
        CancellationToken cancellationToken, [FromQuery] int dias = 7, [FromQuery] int pagina = 1, [FromQuery] int tamano = 50) =>
        Ok(await auditoria.MiActividadAsync(UsuarioId(), dias, pagina, tamano, cancellationToken));

    private Guid UsuarioId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId)
            ? usuarioId
            : throw new UnauthorizedAccessException("Sesión inválida.");
}
