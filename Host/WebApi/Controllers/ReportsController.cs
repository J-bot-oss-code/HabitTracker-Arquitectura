using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Reports.Application.DTOs;
using Reports.Application.Interfaces;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// Reportes con agregación (RF-REP-01 a 05). La identidad y el rol salen del token: un Estándar solo obtiene lo suyo y un
/// Administrador lo de todas las personas, sin que el cliente pueda elegir (RD-06). Ambos exigen <c>desde</c> y <c>hasta</c>
/// (<c>AAAA-MM-DD</c>, UTC, inclusivos). La lógica vive en <see cref="IReportesService"/> (RD-02).
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReportsController(IReportesService reportes) : ControllerBase
{
    /// <summary>Solicitudes de permiso por estado y período (reporte del Core, RF-REP-01).</summary>
    [HttpGet("permissions")]
    [ProducesResponseType(typeof(ReportePermisosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Permisos([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken cancellationToken) =>
        Ok(await reportes.PermisosPorEstadoAsync(Consulta(desde, hasta), cancellationToken));

    /// <summary>«¿Qué tan constante fui en cada plan durante este período?» (reporte del negocio, RF-REP-02).</summary>
    [HttpGet("adherence")]
    [ProducesResponseType(typeof(ReporteConstanciaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Constancia([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken cancellationToken) =>
        Ok(await reportes.ConstanciaPorPlanAsync(Consulta(desde, hasta), cancellationToken));

    private ConsultaReporte Consulta(DateOnly? desde, DateOnly? hasta) =>
        new(UsuarioId(), Correo(), User.IsInRole("Administrador"), desde, hasta);

    private Guid UsuarioId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId)
            ? usuarioId
            : throw new UnauthorizedAccessException("Sesión inválida.");

    private string Correo() =>
        User.FindFirstValue(ClaimTypes.Email) ?? throw new UnauthorizedAccessException("Sesión inválida.");
}
