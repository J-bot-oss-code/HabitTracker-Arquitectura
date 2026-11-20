using Auditing.Application.DTOs;
using Auditing.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// Consulta del historial de auditoría (RF-AUD-07), reservada al Administrador: el rol se exige en el servidor, así que también se
/// rechaza si se invoca directamente (RD-06). Solo tiene <c>GET</c>: no existe ninguna operación para editar ni borrar el historial
/// (RF-AUD-06). Un filtro inválido lo rechaza el servicio con un 400 controlado (RD-07); la lógica vive en
/// <see cref="IRegistroAuditoriaService"/> (RD-02).
/// </summary>
[ApiController]
[Route("api/audit")]
[Authorize(Roles = "Administrador")]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuditController(IRegistroAuditoriaService auditoria) : ControllerBase
{
    /// <summary>
    /// Registros de auditoría, de los más recientes a los más antiguos y paginados. Todos los filtros son opcionales y se combinan:
    /// <c>usuario</c> (el correo contiene el texto), <c>entidad</c>, <c>entidadId</c>, <c>accion</c> y el rango <c>desde</c> y
    /// <c>hasta</c> (<c>AAAA-MM-DD</c>, UTC, ambos días incluidos).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginaAuditoriaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Consultar(
        [FromQuery] string? usuario,
        [FromQuery] string? entidad,
        [FromQuery] string? entidadId,
        [FromQuery] string? accion,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        CancellationToken cancellationToken,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 50) =>
        Ok(await auditoria.ConsultarAsync(
            new ConsultaAuditoria(usuario, entidad, entidadId, accion, desde, hasta, pagina, tamano), cancellationToken));
}
