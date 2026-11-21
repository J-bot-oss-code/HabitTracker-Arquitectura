using System.Net;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Notificaciones;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebApi.Extensions;

namespace WebApi.Controllers;

/// <summary>
/// «Ya lo hice» desde el correo (RF-NEG-07). El enlace no abre sesión: el token firmado dice qué registrar y el servidor lo
/// valida todo de nuevo (RD-06). El <b>GET no tiene efectos</b> (los clientes de correo y los antivirus lo abren solos) y solo
/// muestra una página fija; el <b>POST</b> es el que registra, y es idempotente. Nada de lógica aquí (RD-02).
/// </summary>
[ApiController]
[Route("api/reminders/checkin")]
[AllowAnonymous]
[EnableRateLimiting(LimitacionDePeticionesExtensions.Checkin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class CheckinCorreoController(ICheckinCorreoService checkin) : ControllerBase
{
    /// <summary>Página de confirmación. Sin efectos: solo valida el enlace y dice qué se va a registrar.</summary>
    [HttpGet]
    [Produces("text/html")]
    public async Task<IActionResult> Ver([FromQuery] string? token, CancellationToken cancellationToken)
    {
        try
        {
            var info = await checkin.VerAsync(token ?? string.Empty, cancellationToken);
            return Pagina(StatusCodes.Status200OK, PaginasCheckin.Confirmar(info, token!));
        }
        catch (EnlaceNoValidoException)
        {
            return Pagina(StatusCodes.Status400BadRequest, PaginasCheckin.NoValido());
        }
    }

    /// <summary>Registra el avance. Un segundo POST con el mismo enlace y día no duplica nada.</summary>
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("text/html")]
    public async Task<IActionResult> Registrar([FromForm] string? token, CancellationToken cancellationToken)
    {
        try
        {
            await checkin.EjecutarAsync(token ?? string.Empty, cancellationToken);
            return Pagina(StatusCodes.Status200OK, PaginasCheckin.Listo());
        }
        catch (EnlaceNoValidoException)
        {
            return Pagina(StatusCodes.Status400BadRequest, PaginasCheckin.NoValido());
        }
    }

    // Página fija: sin scripts, sin recursos externos, sin ser enmarcada y sin que el token salga en el Referer.
    private ContentResult Pagina(int estado, string html)
    {
        Response.Headers.ContentSecurityPolicy =
            "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.XContentTypeOptions = "nosniff";
        return new ContentResult { StatusCode = estado, ContentType = "text/html; charset=utf-8", Content = html };
    }
}

/// <summary>Las tres páginas del check-in. Todo lo que viene de datos se codifica: el nombre del hábito lo escribe la persona.</summary>
internal static class PaginasCheckin
{
    public static string Confirmar(InfoCheckin info, string token) => Marco(
        "Registrar tu avance",
        $"""
        <h1>¿Ya lo hiciste?</h1>
        <p><strong>{WebUtility.HtmlEncode(info.Habito)}</strong> · {info.FechaLocal:dd/MM/yyyy}</p>
        <form method="post" action="/api/reminders/checkin">
          <input type="hidden" name="token" value="{WebUtility.HtmlEncode(token)}">
          <button type="submit">Sí, ya lo hice</button>
        </form>
        <p class="nota">Si no lo hiciste, no pasa nada: puedes cerrar esta página.</p>
        """);

    public static string Listo() => Marco(
        "Avance registrado",
        "<h1>¡Listo!</h1><p>Registramos tu avance. Gracias por volver.</p>");

    public static string NoValido() => Marco(
        "Enlace no válido",
        "<h1>Este enlace ya no sirve</h1><p>Puede que haya caducado o que tu plan haya cambiado. Abre la app para registrar tu avance.</p>");

    public static string Marco(string titulo, string cuerpo) => $$"""
        <!doctype html>
        <html lang="es">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width, initial-scale=1">
          <meta name="robots" content="noindex">
          <title>{{titulo}} · HabitTracker Nexus</title>
          <style>
            body { margin: 0; padding: 2rem 1rem; background: #f2f4ef; color: #12261f; font: 1rem/1.55 system-ui, sans-serif; }
            main { max-width: 28rem; margin: 0 auto; }
            h1 { font-size: 1.6rem; line-height: 1.2; margin: 0 0 .75rem; }
            button { min-height: 2.75rem; padding: 0 1.25rem; border: 0; border-radius: 5px; background: #14463a; color: #fff; font: inherit; font-weight: 700; cursor: pointer; }
            .nota { color: #4a5b53; font-size: .9rem; }
          </style>
        </head>
        <body><main>{{cuerpo}}</main></body>
        </html>
        """;
}
