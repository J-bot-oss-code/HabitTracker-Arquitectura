using System.Net;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Notificaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// «Darme de baja» de los recordatorios desde el correo, sin iniciar sesión. Como el check-in: el <b>GET no tiene efectos</b>
/// (los antivirus abren los enlaces solos) y solo muestra una página con el botón; el <b>POST</b> apaga los recordatorios de
/// todos los planes y es idempotente. Nada de lógica aquí (RD-02).
/// </summary>
[ApiController]
[Route("api/reminders/unsubscribe")]
[AllowAnonymous]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Checkin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class BajaRecordatoriosController(IRecordatoriosService recordatorios) : ControllerBase
{
    /// <summary>Página con el botón de baja. Sin efectos.</summary>
    [HttpGet]
    [Produces("text/html")]
    public IActionResult Ver([FromQuery] string? token) =>
        Pagina(PaginasCheckin.Marco(
            "Dejar de recibir recordatorios",
            $"""
            <h1>¿Dejar de recibir recordatorios?</h1>
            <p>Dejaremos de enviarte los recordatorios de tus planes. Puedes volver a activarlos cuando quieras desde la app.</p>
            <form method="post" action="/api/reminders/unsubscribe">
              <input type="hidden" name="token" value="{WebUtility.HtmlEncode(token ?? string.Empty)}">
              <button type="submit">Sí, darme de baja</button>
            </form>
            """));

    /// <summary>Apaga los recordatorios. Repetirlo no cambia nada.</summary>
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("text/html")]
    public async Task<IActionResult> DarDeBaja([FromForm] string? token, CancellationToken cancellationToken)
    {
        try
        {
            await recordatorios.DarDeBajaAsync(token ?? string.Empty, cancellationToken);
            return Pagina(PaginasCheckin.Marco(
                "Listo",
                "<h1>Listo</h1><p>Ya no te enviaremos recordatorios. Si cambias de idea, actívalos en tu plan, en la app.</p>"));
        }
        catch (EnlaceNoValidoException)
        {
            return Pagina(PaginasCheckin.Marco(
                "Enlace no válido",
                "<h1>Este enlace ya no sirve</h1><p>Puede que haya caducado. Apaga los recordatorios desde tu plan, en la app.</p>"),
                StatusCodes.Status400BadRequest);
        }
    }

    // Las mismas cabeceras que el check-in: sin scripts, sin ser enmarcada y sin que el token salga en el Referer.
    private ContentResult Pagina(string html, int estado = StatusCodes.Status200OK)
    {
        Response.Headers.ContentSecurityPolicy =
            "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.XContentTypeOptions = "nosniff";
        return new ContentResult { StatusCode = estado, ContentType = "text/html; charset=utf-8", Content = html };
    }
}
