using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions.Application.DTOs;
using Permissions.Application.Interfaces;

namespace WebApi.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController : ControllerBase
{
    private readonly ISolicitudPermisoService _service;

    public PermissionsController(ISolicitudPermisoService service)
    {
        _service = service;
    }

    /// <summary>Crea una solicitud de permiso (RF-GP-01).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Crear([FromBody] CrearPermisoRequest request)
    {
        var id = await _service.CrearAsync(
            new CrearSolicitudPermisoCommand(ObtenerEmail(), request.PermisoSolicitado));

        return CreatedAtAction(nameof(ObtenerPorId), new { id }, new { id });
    }

    /// <summary>Consulta solicitudes con filtro por rol (RF-GP-02).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SolicitudPermisoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Consultar([FromQuery] bool soloPendientes = true)
    {
        var resultado = await _service.ConsultarAsync(
            new ObtenerSolicitudesQuery(ObtenerEmail(), ObtenerRol(), soloPendientes));

        return Ok(resultado);
    }

    /// <summary>Comprueba si un permiso solicitado ya está aplicado al usuario autenticado (RF-GP-05).</summary>
    [HttpGet("effective/{permiso}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ConsultarPermisoEfectivo(
        [FromRoute, Required, StringLength(256, MinimumLength = 1)] string permiso)
    {
        var tienePermiso = await _service.TienePermisoEfectivoAsync(ObtenerEmail(), permiso);
        return tienePermiso
            ? Ok(new { permiso, efectivo = true })
            : Forbid();
    }

    /// <summary>Consulta el historial propio mediante una acción protegida por el permiso aplicado (RF-GP-05).</summary>
    [HttpGet("actions/{permiso}/execute")]
    [ProducesResponseType(typeof(IEnumerable<SolicitudPermisoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> EjecutarAccionProtegida(
        [FromRoute, Required, StringLength(256, MinimumLength = 1)] string permiso)
    {
        try
        {
            var resultado = await _service.EjecutarAccionProtegidaAsync(ObtenerEmail(), permiso);
            return Ok(resultado);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>Consulta el detalle de una solicitud (RF-GP-02).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SolicitudPermisoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(Guid id)
    {
        // El 404 de una solicitud que no existe lo responde el middleware (SolicitudNoEncontradaException).
        var resultado = await _service.ObtenerPorIdAsync(id, ObtenerEmail(), ObtenerRol());
        return Ok(resultado);
    }

    /// <summary>Aprueba una solicitud pendiente (RF-GP-03, RF-GP-05, RF-GP-07).</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Aprobar(Guid id)
    {
        await _service.AprobarAsync(new AprobarSolicitudCommand(id, ObtenerEmail()));
        return Ok(new { mensaje = "Solicitud aprobada y aplicada correctamente." });
    }

    /// <summary>Rechaza una solicitud pendiente (RF-GP-04, RF-GP-07).</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rechazar(Guid id, [FromBody] RechazarPermisoRequest request)
    {
        await _service.RechazarAsync(new RechazarSolicitudCommand(id, ObtenerEmail(), request.MotivoRechazo));
        return Ok(new { mensaje = "Solicitud rechazada correctamente." });
    }

    private string ObtenerEmail() =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Sesión inválida.");

    private string ObtenerRol() =>
        User.FindFirstValue(ClaimTypes.Role)
        ?? throw new UnauthorizedAccessException("Sesión inválida.");
}

/// <summary>La clave del permiso es un nombre con puntos (<c>Habits.AdvancedReconfig</c>), no texto libre (RD-07).</summary>
public record CrearPermisoRequest(
    [param: Required, StringLength(256, MinimumLength = 3), RegularExpression(@"^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z0-9]+)+$",
        ErrorMessage = "El permiso no es válido.")]
    string PermisoSolicitado);

public record RechazarPermisoRequest(string MotivoRechazo);
