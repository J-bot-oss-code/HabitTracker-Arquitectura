using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Middlewares;

/// <summary>
/// Intercepta peticiones autenticadas y rechaza con 401 los tokens
/// invalidados (logout / cambio de contraseña) y las sesiones de
/// usuarios revocados (desactivación) — RF-CA-18, RF-CA-12, RF-CA-20.
/// </summary>
public class TokenBlacklistMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TokenBlacklistMiddleware> _logger;

    public TokenBlacklistMiddleware(RequestDelegate next, ILogger<TokenBlacklistMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AccessControl.Application.Interfaces.ITokenBlacklist blacklist)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var token = ExtraerBearer(context);

            if (!string.IsNullOrWhiteSpace(token) && blacklist.EstaInvalidado(token))
            {
                _logger.LogWarning("Token invalidado usado en {Path}.", context.Request.Path);
                await RechazarAsync(context);
                return;
            }

            var sub = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (Guid.TryParse(sub, out var usuarioId) && blacklist.UsuarioRevocado(usuarioId))
            {
                _logger.LogWarning("Usuario revocado {UsuarioId} intentó acceder a {Path}.", usuarioId, context.Request.Path);
                await RechazarAsync(context);
                return;
            }
        }

        await _next(context);
    }

    private static async Task RechazarAsync(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Status = (int)HttpStatusCode.Unauthorized,
            Title = "Sesión inválida.",
            Detail = "La sesión ya no es válida. Inicie sesión nuevamente.",
            Instance = context.TraceIdentifier
        };

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }

    private static string? ExtraerBearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return header["Bearer ".Length..].Trim();
    }
}
