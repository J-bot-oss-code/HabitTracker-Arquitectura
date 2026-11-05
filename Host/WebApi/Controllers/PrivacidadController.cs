using System.Security.Claims;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Centro de privacidad: consentimientos legales de la persona autenticada. La identidad sale siempre del token, nunca de
/// la petición (RD-06). Está abierto a cualquier usuario autenticado: un administrador también acepta los documentos.
/// </summary>
[ApiController]
[Route("api/privacy")]
[Authorize]
public class PrivacidadController : ControllerBase
{
    private readonly IConsentimientoService _consentimientos;

    public PrivacidadController(IConsentimientoService consentimientos)
    {
        _consentimientos = consentimientos;
    }

    /// <summary>Consentimientos vigentes y las versiones aceptadas. 404 si la cuenta nunca aceptó nada (cuentas anteriores).</summary>
    [HttpGet("consents")]
    [ProducesResponseType(typeof(ConsentimientosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerConsentimientos()
    {
        var consentimientos = await _consentimientos.ObtenerAsync(ObtenerUsuarioId());

        return consentimientos is null
            ? NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "No encontrado.",
                Detail = "Aún no hay consentimientos registrados para esta cuenta."
            })
            : Ok(consentimientos);
    }

    /// <summary>
    /// Registra los consentimientos. Solo acepta las versiones vigentes de los documentos o las que la persona ya había
    /// aceptado. La fecha de aceptación la fija el servidor.
    /// </summary>
    [HttpPut("consents")]
    [ProducesResponseType(typeof(ConsentimientosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GuardarConsentimientos([FromBody] GuardarConsentimientosRequest solicitud)
    {
        var guardado = await _consentimientos.GuardarAsync(ObtenerUsuarioId(), solicitud);
        return Ok(guardado);
    }

    private Guid ObtenerUsuarioId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            throw new UnauthorizedAccessException("Sesión inválida.");
        }

        return usuarioId;
    }
}
