using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Reports.Application.DTOs;
using Reports.Application.Interfaces;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// Métricas agregadas y anónimas del producto para el Resumen del Administrador. El rol se exige en el servidor (RD-06). Solo
/// lectura y sin parámetros: no hay forma de pedir datos de una persona concreta, y la cohorte mínima la aplica el servicio.
/// </summary>
[ApiController]
[Route("api/admin/metrics")]
[Authorize(Roles = "Administrador")]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Usuario)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class MetricasAdminController(IMetricasProductoService metricas) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(MetricasProductoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Obtener(CancellationToken cancellationToken) =>
        Ok(await metricas.ObtenerAsync(cancellationToken));
}
