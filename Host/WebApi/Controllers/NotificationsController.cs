using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// Bandeja interna de la persona (RF-NOT-04 y RF-NOT-05) y su preferencia de correo (RF-NOT-07). La identidad sale siempre del
/// token: nadie consulta ni marca la notificación de otra persona (RD-06). La lógica vive en <see cref="INotificacionService"/> (RD-02).
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationsController(INotificacionService notificaciones) : ControllerBase
{
    /// <summary>Las notificaciones propias, de la más reciente a la más antigua. <c>?soloNoLeidas=true</c> deja solo las nuevas.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<NotificacionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Listar([FromQuery] bool soloNoLeidas, CancellationToken cancellationToken) =>
        Ok(await notificaciones.ListarAsync(UsuarioId(), soloNoLeidas, cancellationToken));

    /// <summary>Marca una notificación propia como leída. Una ajena se rechaza (403); una que no existe, 404.</summary>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarLeida(Guid id, CancellationToken cancellationToken)
    {
        await notificaciones.MarcarLeidaAsync(UsuarioId(), id, cancellationToken);
        return NoContent();
    }

    [HttpGet("preferences")]
    [ProducesResponseType(typeof(PreferenciasNotificacionesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerPreferencias(CancellationToken cancellationToken) =>
        Ok(await notificaciones.ObtenerPreferenciasAsync(UsuarioId(), cancellationToken));

    /// <summary>Activa o desactiva el correo. Con el correo desactivado las notificaciones siguen apareciendo aquí (RF-NOT-07).</summary>
    [HttpPut("preferences")]
    [ProducesResponseType(typeof(PreferenciasNotificacionesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarPreferencias([FromBody] GuardarPreferenciasRequest request, CancellationToken cancellationToken) =>
        Ok(await notificaciones.GuardarPreferenciasAsync(UsuarioId(), request.CorreoActivo!.Value, cancellationToken));

    private Guid UsuarioId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId)
            ? usuarioId
            : throw new UnauthorizedAccessException("Sesión inválida.");
}

// Nullable para que un cuerpo sin `correoActivo` sea 400 y no un «false» silencioso que apague el correo.
public record GuardarPreferenciasRequest([param: Required] bool? CorreoActivo);
