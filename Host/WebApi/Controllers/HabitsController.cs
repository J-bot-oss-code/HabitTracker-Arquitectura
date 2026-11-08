using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Hábitos del usuario autenticado (RF-NEG-02). Solo traduce HTTP al servicio (RD-02): las reglas viven en el dominio y
/// en <see cref="IHabitoService"/>. Los errores los traduce GlobalExceptionMiddleware: un hábito inexistente es 404
/// (<c>habito_no_encontrado</c>), uno ajeno 403 y una transición prohibida 400.
/// </summary>
[ApiController]
[Route("api/habits")]
[Authorize(Roles = "Estandar")]
public class HabitsController : ControllerBase
{
    private readonly IHabitoService _service;

    public HabitsController(IHabitoService service)
    {
        _service = service;
    }

    /// <summary>Crea un hábito para el usuario autenticado (RF-NEG-02.1). La meta es opcional.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Crear([FromBody] CrearHabitoRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioId();
        var command = new CrearHabitoCommand(request.Nombre, request.Periodicidad, request.CantidadObjetivo, request.Unidad, request.MetaId);
        var id = await _service.CrearHabitoAsync(command, usuarioId, cancellationToken);
        return CreatedAtAction(nameof(Obtener), new { id }, new { id });
    }

    /// <summary>Consulta los hábitos del usuario autenticado (RF-NEG-02.2).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HabitoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Consultar(CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioId();
        return Ok(await _service.ConsultarHabitosAsync(usuarioId, cancellationToken));
    }

    /// <summary>Consulta un hábito del usuario autenticado con sus registros de avance (RF-NEG-02.2).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HabitoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioId();
        return Ok(await _service.ObtenerHabitoAsync(id, usuarioId, cancellationToken));
    }

    /// <summary>Cambia el estado de un hábito (RF-NEG-02.3).</summary>
    [HttpPost("{id:guid}/transition")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoHabitoRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioId();
        await _service.CambiarEstadoAsync(id, request.NuevoEstado, usuarioId, cancellationToken);
        return NoContent();
    }

    /// <summary>Registra avance diario (RF-NEG-02.4). Una fecha futura se rechaza con <c>fecha_futura</c>.</summary>
    [HttpPost("{id:guid}/progress")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarAvance(Guid id, [FromBody] RegistrarAvanceRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioId();
        await _service.RegistrarAvanceAsync(id, request.Cantidad, request.FechaUtc, usuarioId, cancellationToken);
        return NoContent();
    }

    /// <summary>Reconfigura un hábito con permiso aplicado (RF-NEG-02.5).</summary>
    [HttpPost("{id:guid}/advanced-reconfig")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reconfigurar(Guid id, [FromBody] ReconfigurarRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = ObtenerUsuarioId();
        var correo = User.FindFirstValue(ClaimTypes.Email)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");
        await _service.ReconfigurarHabitoAsync(id, request.Periodicidad, request.CantidadObjetivo, request.Unidad, usuarioId, correo, cancellationToken);
        return NoContent();
    }

    /// <summary>La identidad sale siempre del token (RD-06), nunca del cuerpo de la petición.</summary>
    private Guid ObtenerUsuarioId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var usuarioId))
            throw new UnauthorizedAccessException("Sesión inválida.");

        return usuarioId;
    }
}

public record CrearHabitoRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Nombre,
    [Required] Periodicidad Periodicidad,
    [Range(0.01, double.MaxValue)] decimal CantidadObjetivo,
    [Required, StringLength(50, MinimumLength = 1)] string Unidad,
    Guid? MetaId = null);

public record CambiarEstadoHabitoRequest([Required] EstadoHabito NuevoEstado);

public record RegistrarAvanceRequest(
    [Range(0.01, double.MaxValue)] decimal Cantidad,
    [Required] DateTime FechaUtc);

public record ReconfigurarRequest(
    [Required] Periodicidad Periodicidad,
    [Range(0.01, double.MaxValue)] decimal CantidadObjetivo,
    [Required, StringLength(50, MinimumLength = 1)] string Unidad);
