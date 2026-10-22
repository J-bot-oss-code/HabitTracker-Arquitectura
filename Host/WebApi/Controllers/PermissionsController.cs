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

    /// <summary>Consulta el detalle de una solicitud (RF-GP-02).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SolicitudPermisoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerPorId(Guid id)
    {
        try
        {
            var resultado = await _service.ObtenerPorIdAsync(id, ObtenerEmail(), ObtenerRol());
            return Ok(resultado);
        }
        catch (InvalidOperationException ex) when (EsNoEncontrado(ex))
        {
            return NoEncontrado(ex.Message);
        }
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
        try
        {
            await _service.AprobarAsync(new AprobarSolicitudCommand(id, ObtenerEmail()));
            return Ok(new { mensaje = "Solicitud aprobada y aplicada correctamente." });
        }
        catch (InvalidOperationException ex) when (EsNoEncontrado(ex))
        {
            return NoEncontrado(ex.Message);
        }
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
        try
        {
            await _service.RechazarAsync(new RechazarSolicitudCommand(id, ObtenerEmail(), request.MotivoRechazo));
            return Ok(new { mensaje = "Solicitud rechazada correctamente." });
        }
        catch (InvalidOperationException ex) when (EsNoEncontrado(ex))
        {
            return NoEncontrado(ex.Message);
        }
    }

    private string ObtenerEmail() =>
        User.FindFirstValue(ClaimTypes.Email)
        ?? throw new UnauthorizedAccessException("Sesión inválida.");

    private string ObtenerRol() =>
        User.FindFirstValue(ClaimTypes.Role)
        ?? throw new UnauthorizedAccessException("Sesión inválida.");

    private static bool EsNoEncontrado(InvalidOperationException ex) =>
        ex.Message.Contains("no existe", StringComparison.OrdinalIgnoreCase);

    private ObjectResult NoEncontrado(string detalle) => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "No encontrado.",
        Detail = detalle,
        Instance = HttpContext.TraceIdentifier
    });
}

public record CrearPermisoRequest(string PermisoSolicitado);

public record RechazarPermisoRequest(string MotivoRechazo);
