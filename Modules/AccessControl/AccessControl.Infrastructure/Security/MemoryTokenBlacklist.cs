using AccessControl.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Security;

public class MemoryTokenBlacklist : ITokenBlacklist
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MemoryTokenBlacklist> _logger;

    public MemoryTokenBlacklist(IMemoryCache cache, IConfiguration configuration, ILogger<MemoryTokenBlacklist> logger)
    {
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public void InvalidarToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("El token no puede estar vacío.", nameof(token));
        }

        // La entrada expira con la misma vida del token: un token vencido ya no necesita estar en la lista.
        _cache.Set(token, true, TimeSpan.FromMinutes(LeerMinutosExpiracion()));

        _logger.LogInformation("Token invalidado y agregado a la blacklist.");
    }

    public bool EstaInvalidado(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _cache.TryGetValue(token, out _);
    }

    public void RevocarUsuario(Guid usuarioId)
    {
        _cache.Set(LlaveUsuario(usuarioId), true, TimeSpan.FromMinutes(LeerMinutosExpiracion()));

        _logger.LogInformation("Usuario {UsuarioId} revocado: sus sesiones activas dejan de ser válidas.", usuarioId);
    }

    public bool UsuarioRevocado(Guid usuarioId)
    {
        return _cache.TryGetValue(LlaveUsuario(usuarioId), out _);
    }

    private static string LlaveUsuario(Guid usuarioId) => $"usuario-revocado:{usuarioId}";

    private int LeerMinutosExpiracion()
    {
        var minutos = _configuration.GetValue<int>($"{JwtSettings.Seccion}:MinutosExpiracion");
        return minutos > 0 ? minutos : 60;
    }
}
