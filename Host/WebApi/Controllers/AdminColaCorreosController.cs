using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// Cola de correos para el Administrador: consulta de cada correo (RF-NOT-11) y una pasada manual del procesador (RF-NOT-09).
/// Un Estándar recibe 403 (RD-06). La lógica vive en <see cref="IColaCorreosAdmin"/> (RD-02).
/// </summary>
[ApiController]
[Route("api/admin/email-queue")]
[Authorize(Roles = "Administrador")]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminColaCorreosController(IColaCorreosAdmin cola) : ControllerBase
{
    /// <summary>Estado, intentos, fechas y último error de cada correo (sin el cuerpo). <c>?estado=Pendiente|Procesando|Enviado|Fallido</c>.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CorreoColaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar([FromQuery] string? estado, CancellationToken cancellationToken) =>
        Ok(await cola.ListarAsync(estado, cancellationToken));

    /// <summary>Ejecuta una pasada del procesador sin esperar a los 15 segundos del servicio en segundo plano.</summary>
    [HttpPost("process")]
    [ProducesResponseType(typeof(ResultadoColaDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Procesar(CancellationToken cancellationToken) =>
        Ok(await cola.ProcesarAsync(cancellationToken));
}
