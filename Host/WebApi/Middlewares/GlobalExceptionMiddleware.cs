using System.Net;
using System.Text.Json;
using AccessControl.Domain.Excepciones;
using DocumentManager.Application.Exceptions;
using HabitTracker.Application.Exceptions;
using HabitTracker.Domain.Excepciones;
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
            // Se registra el endpoint (la plantilla de la ruta), no la ruta cruda: esa la controla quien llama (cs/log-forging).
            _logger.LogInformation("Solicitud cancelada por el cliente en {Endpoint}.", context.GetEndpoint()?.DisplayName ?? "(sin ruta)");
        }
        catch (CredencialesInvalidasException ex)
        {
            // Debe ir antes de UnauthorizedAccessException (es su subclase). En el inicio de sesión no hay usuario
            // autenticado, así que siempre es 401 y el texto no dice si falló el correo o la contraseña (RF-CA-03).
            _logger.LogInformation(ex, "Credenciales incorrectas.");
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Credenciales incorrectas.", "Correo o contraseña incorrectos.");
        }
        catch (ProveedorExternoNoDisponibleException ex)
        {
            // Falta Google:ClientId o Google no responde. Solo esta operación se rechaza; el resto de la API sigue funcionando.
            _logger.LogWarning("Proveedor de inicio de sesión no disponible.");
            await WriteProblemAsync(context, HttpStatusCode.ServiceUnavailable, "Servicio no disponible.", ex.Message, ex.Codigo);
        }
        catch (TokenExternoInvalidoException ex)
        {
            // Debe ir antes de UnauthorizedAccessException (es su subclase). Siempre 401, sin decir qué falló del token.
            _logger.LogInformation("Token del proveedor externo rechazado.");
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Credenciales incorrectas.", ex.Message, ex.Codigo);
        }
        catch (VinculacionRequeridaException ex)
        {
            // Debe ir antes de ControlAccesoException (es su clase base). 409: la cuenta existe y hay que confirmar la contraseña.
            _logger.LogInformation("Vinculación con el proveedor externo pendiente de confirmar.");
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "Vinculación requerida.", ex.Message, ex.Codigo);
        }
        catch (SesionInvalidaException ex)
        {
            // Debe ir antes de UnauthorizedAccessException (es su subclase). El refresh token no sirve: siempre 401, con el código
            // que le dice al cliente que debe iniciar sesión de nuevo.
            _logger.LogInformation("Refresh token rechazado.");
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Sesión inválida.", ex.Message, ex.Codigo);
        }
        catch (OperacionNoPermitidaException ex)
        {
            // Debe ir antes de ControlAccesoException e InvalidOperationException (son sus clases base).
            _logger.LogInformation(ex, "Operación no permitida por el rol del ejecutor.");
            await WriteProblemAsync(context, HttpStatusCode.Forbidden, "No autorizado.", ex.Message, ex.Codigo);
        }
        catch (ControlAccesoException ex)
        {
            // Reglas de Control de Acceso con código estable ("cuenta_no_activa", "cuenta_bloqueada"...): el cliente
            // distingue el motivo (RF-CA-15) sin que el mensaje revele datos internos (RD-08).
            _logger.LogInformation(ex, "Operación de Control de Acceso rechazada: {Codigo}.", ex.Codigo);
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "La operación no pudo completarse.", ex.Message, ex.Codigo);
        }
        catch (RecursoNoEncontradoException ex)
        {
            // Debe ir antes de HabitTrackerException e InvalidOperationException (son sus clases base). Siempre con cuerpo:
            // el frontend toma un 404 vacío como "endpoint inexistente" (esNoDisponible).
            _logger.LogInformation("Recurso del módulo de negocio no encontrado: {Codigo}.", ex.Codigo);
            await WriteProblemAsync(context, HttpStatusCode.NotFound, "No encontrado.", ex.Message, ex.Codigo);
        }
        catch (HabitTrackerException ex)
        {
            // Reglas del módulo de negocio con código estable ("fecha_futura", "meta_no_encontrada"...). El mensaje lo
            // escribe el propio módulo y no lleva datos internos (RD-08).
            _logger.LogInformation("Operación del módulo de negocio rechazada: {Codigo}.", ex.Codigo);
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "La operación no pudo completarse.", ex.Message, ex.Codigo);
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
            _logger.LogError(ex, "Excepción no manejada en {Endpoint}.", context.GetEndpoint()?.DisplayName ?? "(sin ruta)");
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError, "Ocurrió un error inesperado.", "Ocurrió un error inesperado. Inténtelo de nuevo más tarde.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, HttpStatusCode status, string title, string detail, string? codigo = null)
    {
        var problem = new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Instance = context.TraceIdentifier
        };

        if (codigo is not null)
        {
            problem.Extensions["codigo"] = codigo;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)status;

        var payload = JsonSerializer.Serialize(problem);
        await context.Response.WriteAsync(payload);
    }
}
