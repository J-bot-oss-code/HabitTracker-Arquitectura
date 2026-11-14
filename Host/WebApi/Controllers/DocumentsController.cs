using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;
using System.Security.Claims;
using DocumentManager.Application.DTOs;
using DocumentManager.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

/// <summary>
/// Manejador de documentos (Pieza 3 del Core). [Authorize] a nivel de clase: TODAS las operaciones exigen
/// sesión válida (RD-06). Este controlador solo traduce HTTP a llamadas del servicio; las reglas de acceso,
/// validación y borrado viven en DocumentoService y en el dominio (RD-02). Los errores del módulo
/// (404, 400 de archivo, 403) los traduce GlobalExceptionMiddleware.
/// </summary>
[ApiController]
[Route("api/documents")]
[Authorize]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
public class DocumentsController : ControllerBase
{
    // Un poco más que el máximo del archivo (10 MB) para dejar sitio a los bytes del multipart.
    private const long LimiteCuerpoBytes = 11 * 1024 * 1024;

    private readonly IDocumentoService _service;

    public DocumentsController(IDocumentoService service)
    {
        _service = service;
    }

    /// <summary>Sube un documento (RF-DOC-01). Guarda nombre, tipo, tamaño, fecha UTC y quién lo subió.</summary>
    [HttpPost]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Subidas)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(LimiteCuerpoBytes)]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Subir(IFormFile? archivo, CancellationToken cancellationToken)
    {
        var (usuarioId, correo, _) = ObtenerIdentidad();

        // Si no llega archivo, Contenido queda en null y el servicio responde con un rechazo controlado (RD-07).
        await using var contenido = archivo?.OpenReadStream();
        var dto = await _service.SubirAsync(
            new SubirDocumentoCommand(usuarioId, correo, archivo?.FileName ?? string.Empty, contenido),
            cancellationToken);

        return CreatedAtAction(nameof(Obtener), new { id = dto.Id }, dto);
    }

    /// <summary>Lista los documentos que el usuario puede ver, sin eliminados (RF-DOC-02).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DocumentoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
    {
        var (usuarioId, _, esAdministrador) = ObtenerIdentidad();
        return Ok(await _service.ListarAsync(usuarioId, esAdministrador, cancellationToken));
    }

    /// <summary>Consulta el registro de un documento con todos sus datos (RF-DOC-01).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken cancellationToken)
    {
        var (usuarioId, _, esAdministrador) = ObtenerIdentidad();
        return Ok(await _service.ObtenerAsync(id, usuarioId, esAdministrador, cancellationToken));
    }

    /// <summary>Descarga el archivo. Solo el dueño o un Administrador (RF-DOC-03).</summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(typeof(FileStreamResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Descargar(Guid id, CancellationToken cancellationToken)
    {
        var (usuarioId, _, esAdministrador) = ObtenerIdentidad();
        var archivo = await _service.DescargarAsync(id, usuarioId, esAdministrador, cancellationToken);

        // nosniff: el navegador respeta el tipo que declaramos y no intenta adivinar que es HTML.
        Response.Headers.XContentTypeOptions = "nosniff";

        // Son documentos privados: que ninguna caché intermedia los conserve.
        Response.Headers.CacheControl = "no-store";

        // File(...) con nombre responde con Content-Disposition: attachment, con el nombre bien codificado.
        return File(archivo.Contenido, archivo.TipoContenido, archivo.NombreArchivo);
    }

    /// <summary>Elimina de forma lógica: deja de listarse pero el registro permanece (RF-DOC-04).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid id, CancellationToken cancellationToken)
    {
        var (usuarioId, _, esAdministrador) = ObtenerIdentidad();
        await _service.EliminarAsync(id, usuarioId, esAdministrador, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// La identidad sale SIEMPRE de los claims firmados del token, nunca del cuerpo ni de la URL (RD-06):
    /// por eso invocar la operación directamente con el id de otro usuario tampoco funciona.
    /// </summary>
    private (Guid UsuarioId, string Correo, bool EsAdministrador) ObtenerIdentidad()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");
        var correo = User.FindFirstValue(ClaimTypes.Email)
            ?? throw new UnauthorizedAccessException("Sesión inválida.");

        if (!Guid.TryParse(sub, out var usuarioId))
        {
            throw new UnauthorizedAccessException("Sesión inválida.");
        }

        return (usuarioId, correo, User.IsInRole("Administrador"));
    }
}
