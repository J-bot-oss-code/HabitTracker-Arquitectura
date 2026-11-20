using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HabitTracker.Application.Sugerencias;
using HabitTracker.Domain.Sugerencias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// Quejas y propuestas de mejora. La persona Estándar envía y ve las suyas (<c>api/feedback</c>); el Administrador revisa la
/// bandeja y marca el estado (<c>api/admin/feedback</c>). El rol se exige en el servidor (RD-06) y la persona sale del token; la
/// lógica vive en <see cref="ISugerenciasService"/> (RD-02). El texto se guarda tal cual y siempre se muestra como texto (RD-07).
/// </summary>
[ApiController]
[Authorize]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SugerenciasController(ISugerenciasService sugerencias) : ControllerBase
{
    /// <summary>Envía una queja o una propuesta de mejora. Como mucho 5 en 24 horas (409 si se pasa: <c>limite_sugerencias</c>).</summary>
    [HttpPost("api/feedback")]
    [Authorize(Roles = "Estandar")]
    [ProducesResponseType(typeof(SugerenciaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enviar([FromBody] EnviarSugerenciaRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            await sugerencias.EnviarAsync(UsuarioId(), request.Tipo!.Value, request.Texto, cancellationToken));

    /// <summary>Lo que envió la propia persona, de lo más reciente a lo más antiguo.</summary>
    [HttpGet("api/feedback")]
    [Authorize(Roles = "Estandar")]
    [ProducesResponseType(typeof(IReadOnlyList<SugerenciaDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarMias(CancellationToken cancellationToken) =>
        Ok(await sugerencias.ListarMiasAsync(UsuarioId(), cancellationToken));

    /// <summary>La bandeja del Administrador, paginada y con filtro opcional por estado (Nueva, Leida, Resuelta).</summary>
    [HttpGet("api/admin/feedback")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(PaginaSugerenciasDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar(
        [FromQuery] EstadoSugerencia? estado, CancellationToken cancellationToken, [FromQuery] int pagina = 1, [FromQuery] int tamano = 20) =>
        Ok(await sugerencias.ListarAsync(estado, pagina, tamano, cancellationToken));

    /// <summary>Marca una sugerencia como leída o resuelta. Repetir el mismo estado no cambia nada.</summary>
    [HttpPost("api/admin/feedback/{id:guid}/status")]
    [Authorize(Roles = "Administrador")]
    [ProducesResponseType(typeof(SugerenciaAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoSugerenciaRequest request, CancellationToken cancellationToken) =>
        Ok(await sugerencias.CambiarEstadoAsync(id, request.Estado!.Value, cancellationToken));

    private Guid UsuarioId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId)
            ? usuarioId
            : throw new UnauthorizedAccessException("Sesión inválida.");
}

// Nullable para que un cuerpo sin tipo o sin estado sea 400 y no un valor por defecto silencioso.
public record EnviarSugerenciaRequest(
    [param: Required] TipoSugerencia? Tipo,
    [param: Required, StringLength(Sugerencia.LongitudMaxima)] string Texto);

public record CambiarEstadoSugerenciaRequest([param: Required] EstadoSugerencia? Estado);
