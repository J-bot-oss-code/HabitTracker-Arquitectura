using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Middlewares;

/// <summary>
/// Middleware global de excepciones (RD-07, RD-08).
/// Traduce excepciones de negocio a ProblemDetails (RFC 7807) sin exponer
/// stack traces, rutas ni consultas al cliente.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Solicitud inválida.", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "La operación no pudo completarse.", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción no manejada en {Path}.", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "Ocurrió un error inesperado.", "Ocurrió un error inesperado. Inténtelo de nuevo más tarde.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, HttpStatusCode status, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Instance = context.TraceIdentifier
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)status;

        var payload = JsonSerializer.Serialize(problem);
        await context.Response.WriteAsync(payload);
    }
}
