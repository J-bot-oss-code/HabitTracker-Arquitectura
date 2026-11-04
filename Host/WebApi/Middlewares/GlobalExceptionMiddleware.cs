using System.Net;
using System.Text.Json;
using DocumentManager.Application.Exceptions;
using HabitTracker.Application.Exceptions;
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
        catch (ServicioClimaNoDisponibleException)
        {
            // La API externa falló o no hay clave. Solo esta operación se rechaza; el resto del proyecto sigue funcionando.
            _logger.LogWarning("Servicio de clima no disponible.");
            await WriteProblemAsync(context, HttpStatusCode.ServiceUnavailable, "Servicio no disponible.",
                "El servicio de clima no está disponible en este momento. Inténtelo de nuevo más tarde.");
        }
        catch (CiudadNoEncontradaException)
        {
            await WriteProblemAsync(context, HttpStatusCode.NotFound, "No encontrado.", "No se encontró la ciudad indicada.");
        }
        catch (DocumentoNoEncontradoException ex)
        {
            _logger.LogInformation(ex, "Documento no encontrado.");
            await WriteProblemAsync(context, HttpStatusCode.NotFound, "No encontrado.", "El documento no existe.");
        }
        catch (ArchivoInvalidoException ex)
        {
            // Debe ir antes de ArgumentException (es su subclase). Su mensaje lo escribimos nosotros
            // ("El archivo supera el tamaño máximo...") y es seguro mostrarlo: no lleva rutas ni trazas.
            _logger.LogInformation(ex, "Archivo rechazado.");
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Archivo no válido.", ex.Message);
        }
        catch (BadHttpRequestException ex)
        {
            // Kestrel lo lanza al superar el límite del cuerpo (413) o con un cuerpo mal formado (400).
            _logger.LogInformation(ex, "Solicitud HTTP rechazada por el servidor.");
            var status = (HttpStatusCode)ex.StatusCode;
            var detalle = ex.StatusCode == StatusCodes.Status413PayloadTooLarge
                ? "La solicitud supera el tamaño permitido."
                : "La solicitud no es válida.";
            await WriteProblemAsync(context, status, "Solicitud rechazada.", detalle);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente cerró la conexión a mitad de la petición: no es un error del servidor ni hay a quién responder.
            _logger.LogInformation("Solicitud cancelada por el cliente en {Path}.", context.Request.Path);
        }
        catch (ArgumentException ex)
        {
            _logger.LogInformation(ex, "Solicitud rechazada por datos no válidos.");
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Solicitud inválida.", "La solicitud contiene datos no válidos.");
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogInformation(ex, "Operación no autorizada.");
            var statusCode = context.User.Identity?.IsAuthenticated == true
                ? HttpStatusCode.Forbidden
                : HttpStatusCode.Unauthorized;
            await WriteProblemAsync(context, statusCode, "No autorizado.", "No se pudo autorizar la solicitud.");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogInformation(ex, "Operación rechazada por el estado actual.");
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "La operación no pudo completarse.", "La solicitud no pudo completarse.");
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
