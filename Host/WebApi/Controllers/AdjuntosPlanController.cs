using System.Security.Claims;
using HabitTracker.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace WebApi.Controllers;

/// <summary>
/// Archivos de «Mis archivos» vinculados a los planes de la persona. La identidad sale siempre del token y un plan o archivo ajeno
/// responde 404 (RD-06). La lógica vive en <see cref="IAdjuntosPlanService"/> (RD-02).
/// </summary>
[ApiController]
[Route("api/my-plans")]
[Authorize(Roles = "Estandar")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdjuntosPlanController(IAdjuntosPlanService adjuntos) : ControllerBase
{
    /// <summary>Todos los vínculos de la persona (archivo, plan y si el plan está en curso).</summary>
    [HttpGet("attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<VinculoDocumentoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Vinculos(CancellationToken cancellationToken) =>
        Ok(await adjuntos.ListarVinculosAsync(UsuarioId(), cancellationToken));

    [HttpGet("{id:guid}/attachments")]
    [ProducesResponseType(typeof(IReadOnlyList<AdjuntoPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Listar(Guid id, CancellationToken cancellationToken) =>
        Ok(await adjuntos.ListarDePlanAsync(UsuarioId(), id, cancellationToken));

    /// <summary>Vincula un archivo propio al plan. <c>400 plan_no_en_curso|adjunto_duplicado|limite_adjuntos</c>, 404 si el archivo no es suyo.</summary>
    [HttpPost("{id:guid}/attachments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Adjuntar(Guid id, [FromBody] AdjuntarArchivoRequest request, CancellationToken cancellationToken)
    {
        await adjuntos.AdjuntarAsync(UsuarioId(), id, request.DocumentoId, cancellationToken);
        return NoContent();
    }

    /// <summary>Quita el vínculo; el archivo sigue en «Mis archivos». Con el plan en curso pide el permiso (409 <c>documento_requiere_permiso</c>).</summary>
    [HttpDelete("{id:guid}/attachments/{documentoId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Quitar(Guid id, Guid documentoId, CancellationToken cancellationToken)
    {
        await adjuntos.QuitarAsync(UsuarioId(), Correo(), id, documentoId, cancellationToken);
        return NoContent();
    }

    private Guid UsuarioId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId)
            ? usuarioId
            : throw new UnauthorizedAccessException("Sesión inválida.");

    private string Correo() =>
        User.FindFirstValue(ClaimTypes.Email) ?? throw new UnauthorizedAccessException("Sesión inválida.");
}

public record AdjuntarArchivoRequest([Required] Guid DocumentoId);
