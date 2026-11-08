using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Services;
using HabitTracker.Domain.Progreso;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Peso y medidas corporales (contratos que el frontend ya usaba simulados). Son datos de salud: guardarlos exige el
/// consentimiento <c>datosSalud</c> (409) y solo los ve su dueño (RD-06).
/// </summary>
[ApiController]
[Route("api/progress")]
[Authorize(Roles = "Estandar")]
public class ProgressController(IProgresoCorporalService progreso) : ControllerBase
{
    [HttpGet("weights")]
    [ProducesResponseType(typeof(PesosDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerPesos(CancellationToken cancellationToken) =>
        Ok(await progreso.ObtenerPesosAsync(UsuarioId(), cancellationToken));

    /// <summary>
    /// Registra el peso de un día (uno por día UTC: el nuevo reemplaza al anterior). <c>objetivoKg</c>: si se omite, el
    /// objetivo se conserva; con <c>null</c> se quita; con un número se fija.
    /// </summary>
    [HttpPost("weights")]
    [ProducesResponseType(typeof(PesosDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegistrarPeso([FromBody] RegistrarPesoRequest request, CancellationToken cancellationToken)
    {
        var (cambiar, objetivo) = LeerObjetivo(request.ObjetivoKg);
        var command = new RegistrarPesoCommand(request.Fecha!.Value, request.PesoKg, cambiar, objetivo);
        var pesos = await progreso.RegistrarPesoAsync(UsuarioId(), command, cancellationToken);
        return Created("/api/progress/weights", pesos);
    }

    [HttpGet("measurements")]
    [ProducesResponseType(typeof(IReadOnlyList<MedicionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerMediciones(CancellationToken cancellationToken) =>
        Ok(await progreso.ObtenerMedicionesAsync(UsuarioId(), cancellationToken));

    /// <summary>Registra una medida (una por día UTC y zona: la nueva reemplaza a la anterior).</summary>
    [HttpPost("measurements")]
    [ProducesResponseType(typeof(MedicionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegistrarMedicion([FromBody] RegistrarMedicionRequest request, CancellationToken cancellationToken)
    {
        var command = new RegistrarMedicionCommand(request.Fecha!.Value, request.Zona!.Value, request.Valor);
        var medicion = await progreso.RegistrarMedicionAsync(UsuarioId(), command, cancellationToken);
        return Created("/api/progress/measurements", medicion);
    }

    /// <summary>
    /// Distingue "omitido" (se conserva el objetivo) de <c>null</c> (se quita). Con <c>decimal?</c> los dos llegarían igual.
    /// </summary>
    public static (bool Cambiar, decimal? Objetivo) LeerObjetivo(JsonElement objetivo) => objetivo.ValueKind switch
    {
        JsonValueKind.Undefined => (false, null),
        JsonValueKind.Null => (true, null),
        JsonValueKind.Number when objetivo.TryGetDecimal(out var valor) => (true, valor),
        _ => throw new ArgumentException("El peso objetivo debe ser un número o null.", nameof(objetivo)),
    };

    private Guid UsuarioId()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
            throw new UnauthorizedAccessException("Sesión inválida.");
        return usuarioId;
    }
}

public sealed record RegistrarPesoRequest(
    [Required] DateTime? Fecha,
    [Range(typeof(decimal), "30", "300", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal PesoKg,
    JsonElement ObjetivoKg = default);

public sealed record RegistrarMedicionRequest(
    [Required] DateTime? Fecha,
    [Required] ZonaMedicion? Zona,
    [Range(typeof(decimal), "0.1", "1000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true)] decimal Valor);
