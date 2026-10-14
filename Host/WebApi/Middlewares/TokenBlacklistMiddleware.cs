using System.IdentityModel.Tokens.Jwt;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using System.Text.Json;
using AccessControl.Infrastructure.Security;
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

            if (Guid.TryParse(sub, out var usuarioId) && !blacklist.SesionVigente(usuarioId, EmitidoEn(token)))
            {
                _logger.LogWarning("Sesión revocada del usuario {UsuarioId} usada en {Path}.", usuarioId, context.Request.Path);
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

    private static DateTimeOffset EmitidoEn(string? token)
    {
        // Se lee el iat directo del JWT: no depende del mapeo de claims de ASP.NET Core.
        if (string.IsNullOrWhiteSpace(token))
        {
            return DateTimeOffset.MinValue;
        }

        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var issuedAtTicks = jwt.Claims.FirstOrDefault(c =>
                c.Type == JwtProvider.SessionIssuedAtTicksClaim)?.Value;

            if (long.TryParse(issuedAtTicks, NumberStyles.Integer, CultureInfo.InvariantCulture, out var utcTicks)
                && utcTicks >= DateTime.MinValue.Ticks
                && utcTicks <= DateTime.MaxValue.Ticks)
            {
                return new DateTimeOffset(utcTicks, TimeSpan.Zero);
            }

            // Compatibilidad con tokens emitidos antes de introducir ticks UTC.
            var issuedAtMilliseconds = jwt.Claims.FirstOrDefault(c => c.Type == "session_issued_at_ms")?.Value;
            if (long.TryParse(issuedAtMilliseconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixMilliseconds))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
            }

            return new DateTimeOffset(jwt.IssuedAt, TimeSpan.Zero);
        }
        catch
        {
            return DateTimeOffset.MinValue;
        }
    }

    private static string? ExtraerBearer(HttpContext context)    {
        var header = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(header) || !header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return header["Bearer ".Length..].Trim();
    }
}
