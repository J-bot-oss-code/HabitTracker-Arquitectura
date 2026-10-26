using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/habits")]
[Authorize(Roles = "Estandar")]
public class HabitsController : ControllerBase
{
    private readonly HabitoService _service;

    public HabitsController(HabitoService service)
    {
        _service = service;
    }

    /// <summary>Crea un hábito para el usuario autenticado (RF-NEG-02.1).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Crear([FromBody] CrearHabitoRequest request)
    {
        var (usuarioId, correo) = ObtenerIdentidad();
        var command = new CrearHabitoCommand(request.Nombre, request.Periodicidad, request.CantidadObjetivo, request.Unidad, request.MetaId);
        var id = await _service.CrearHabitoAsync(command, usuarioId);
        return CreatedAtAction(nameof(Consultar), new { id }, new { id });
    }

    /// <summary>Consulta los hábitos del usuario autenticado (RF-NEG-02.2).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<HabitoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Consultar()
    {
        var (usuarioId, _) = ObtenerIdentidad();
        var habitos = await _service.ConsultarHabitosAsync(usuarioId);
        return Ok(habitos);
    }

    /// <summary>Cambia el estado de un hábito (RF-NEG-02.3).</summary>
    [HttpPost("{id:guid}/transition")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoHabitoRequest request)
    {
        var (usuarioId, _) = ObtenerIdentidad();
        await _service.CambiarEstadoAsync(id, request.NuevoEstado, usuarioId);
        return NoContent();
    }

    /// <summary>Registra avance diario (RF-NEG-02.4).</summary>
    [HttpPost("{id:guid}/progress")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RegistrarAvance(Guid id, [FromBody] RegistrarAvanceRequest request)
    {
        var (usuarioId, _) = ObtenerIdentidad();
        await _service.RegistrarAvanceAsync(id, request.Cantidad, request.FechaUtc, usuarioId);
        return NoContent();
    }

    /// <summary>Reconfigura un hábito con permiso aplicado (RF-NEG-02.5).</summary>
    [HttpPost("{id:guid}/advanced-reconfig")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reconfigurar(Guid id, [FromBody] ReconfigurarRequest request)
    {
        var (usuarioId, correo) = ObtenerIdentidad();
        await _service.ReconfigurarHabitoAsync(id, request.Periodicidad, request.CantidadObjetivo, request.Unidad, usuarioId, correo);
        return NoContent();
    }

    private (Guid usuarioId, string correo) ObtenerIdentidad()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");
        var correo = User.FindFirstValue(ClaimTypes.Email)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");

        if (!Guid.TryParse(sub, out var usuarioId))
            throw new UnauthorizedAccessException("Sesión inválida.");

        return (usuarioId, correo);
    }
}

public record CrearHabitoRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Nombre,
    [Required] Periodicidad Periodicidad,
    [Range(0.01, double.MaxValue)] decimal CantidadObjetivo,
    [Required, StringLength(50, MinimumLength = 1)] string Unidad,
    [Required] Guid MetaId);

public record CambiarEstadoHabitoRequest([Required] EstadoHabito NuevoEstado);

public record RegistrarAvanceRequest(
    [Range(0.01, double.MaxValue)] decimal Cantidad,
    [Required] DateTime FechaUtc);

public record ReconfigurarRequest(
    [Required] Periodicidad Periodicidad,
    [Range(0.01, double.MaxValue)] decimal CantidadObjetivo,
    [Required, StringLength(50, MinimumLength = 1)] string Unidad);