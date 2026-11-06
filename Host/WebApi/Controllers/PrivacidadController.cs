using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// Centro de privacidad: consentimientos legales de la persona autenticada. La identidad sale siempre del token, nunca de
/// la petición (RD-06). Los consentimientos están abiertos a cualquier usuario autenticado (un administrador también acepta
/// los documentos); la baja de cuenta y la descarga de datos son para el rol Estandar: darse de baja podría dejar al sistema
/// sin nadie que lo administre.
/// </summary>
[ApiController]
[Route("api/privacy")]
[Authorize]
public class PrivacidadController : ControllerBase
{
    private const string RolEstandar = "Estandar";

    private readonly IConsentimientoService _consentimientos;
    private readonly IServicioPrivacidad _privacidad;

    public PrivacidadController(IConsentimientoService consentimientos, IServicioPrivacidad privacidad)
    {
        _consentimientos = consentimientos;
        _privacidad = privacidad;
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

    /// <summary>
    /// Desactiva la cuenta (<c>desactivar</c>: vuelve a entrar con solo iniciar sesión) o programa su eliminación
    /// (<c>eliminar</c>: se borra de forma definitiva al terminar el periodo de gracia, 30 días por defecto, y se puede
    /// cancelar). Se confirma con la contraseña o, si la cuenta no tiene, con <c>idTokenGoogle</c>. Una confirmación
    /// incorrecta es un 400 que cuenta para el bloqueo (RF-CA-19), nunca un 401. Revoca todas las sesiones.
    /// </summary>
    [HttpPost("deletion-request")]
    [Authorize(Roles = RolEstandar)]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Privacidad)]
    [ProducesResponseType(typeof(BajaSolicitadaDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SolicitarBaja([FromBody] SolicitarBajaRequest solicitud)
    {
        var resultado = await _privacidad.SolicitarBajaAsync(ObtenerUsuarioId(), solicitud.Modo, solicitud.Password, solicitud.IdTokenGoogle);
        return StatusCode(StatusCodes.Status202Accepted, resultado);
    }

    /// <summary>Cuándo se borrará la cuenta si no se cancela. 404 si no hay una baja programada.</summary>
    [HttpGet("deletion-request")]
    [Authorize(Roles = RolEstandar)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerBaja()
    {
        var borradoEn = await _privacidad.ObtenerBajaProgramadaAsync(ObtenerUsuarioId());

        return borradoEn is null
            ? NotFound(SinBaja())
            : Ok(new { borradoEn });
    }

    /// <summary>Cancela la eliminación programada durante el periodo de gracia. No pide la contraseña: sólo conserva la cuenta.</summary>
    [HttpDelete("deletion-request")]
    [Authorize(Roles = RolEstandar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelarBaja()
    {
        return await _privacidad.CancelarBajaAsync(ObtenerUsuarioId())
            ? NoContent()
            : NotFound(SinBaja());
    }

    /// <summary>
    /// Descarga todo lo que el sistema guarda de la persona en un JSON (derecho de acceso y portabilidad): cuenta,
    /// consentimientos, vínculos, sesiones y los datos de cada módulo. Nunca incluye la contraseña ni los tokens.
    /// </summary>
    [HttpGet("export")]
    [Authorize(Roles = RolEstandar)]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Privacidad)]
    [ProducesResponseType(typeof(ExportacionDatosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Exportar(CancellationToken cancelacion)
    {
        var datos = await _privacidad.ExportarAsync(ObtenerUsuarioId(), cancelacion);

        // Son datos personales: que no se guarden en cachés intermedias ni del navegador.
        Response.Headers.CacheControl = "no-store";
        Response.Headers.ContentDisposition = $"attachment; filename=\"habittracker-mis-datos-{datos.GeneradoEn:yyyyMMdd}.json\"";
        return Ok(datos);
    }

    private ProblemDetails SinBaja() => new()
    {
        Status = StatusCodes.Status404NotFound,
        Title = "No encontrado.",
        Detail = "La cuenta no tiene una eliminación programada.",
        Instance = HttpContext.TraceIdentifier
    };

    private Guid ObtenerUsuarioId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            throw new UnauthorizedAccessException("Sesión inválida.");
        }

        return usuarioId;
    }
}

/// <summary>Modo (<c>desactivar</c> o <c>eliminar</c>) y confirmación: la contraseña, o el ID token de Google si la cuenta no tiene.</summary>
public record SolicitarBajaRequest(
    [param: Required, StringLength(20)] string Modo,
    [param: StringLength(1024)] string? Password = null,
    [param: StringLength(4096)] string? IdTokenGoogle = null);
