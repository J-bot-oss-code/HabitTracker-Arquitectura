using System.Security.Cryptography;
using System.Text;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Services;

/// <summary>
/// Blacklist persistente en base de datos: sobrevive reinicios (RD-09).
/// La revocación por usuario guarda una hora de corte: solo mueren los
/// tokens emitidos ANTES del corte, los nuevos siguen sirviendo.
/// </summary>
public class TokenBlacklistService : ITokenBlacklist
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TokenBlacklistService> _logger;

    public TokenBlacklistService(
        ApplicationDbContext dbContext,
        IConfiguration configuration,
        ILogger<TokenBlacklistService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public void InvalidarToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("El token no puede estar vacío.", nameof(token));
        }

        var ahora = DateTime.UtcNow;
        LimpiarExpirados(ahora);

        _dbContext.TokensRevocados.Add(new TokenRevocado(Hash(token), ahora.AddMinutes(LeerMinutosExpiracion())));
        _dbContext.SaveChanges();

        _logger.LogInformation("Token invalidado y agregado a la blacklist.");
    }

    public bool EstaInvalidado(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var ahora = DateTime.UtcNow;
        var hash = Hash(token);

        return _dbContext.TokensRevocados.Any(t => t.TokenHash == hash && t.FechaExpiracion > ahora);
    }

    public void RevocarSesionesAnteriores(Guid usuarioId)
    {
        // El iat del JWT tiene precisión de segundos: el corte se trunca igual
        // para no matar tokens emitidos en el mismo segundo posterior al corte.
        var ahora = TruncarASegundos(DateTime.UtcNow);
        LimpiarExpirados(ahora);

        var expiracion = ahora.AddMinutes(LeerMinutosExpiracion());
        var existente = _dbContext.RevocacionesSesion.FirstOrDefault(r => r.UsuarioId == usuarioId);

        if (existente == null)
        {
            _dbContext.RevocacionesSesion.Add(new RevocacionSesion(usuarioId, ahora, expiracion));
        }
        else
        {
            existente.ActualizarCorte(ahora, expiracion);
        }

        _dbContext.SaveChanges();

        _logger.LogInformation("Sesiones anteriores del usuario {UsuarioId} revocadas.", usuarioId);
    }

    public bool SesionVigente(Guid usuarioId, DateTimeOffset emitidoEn)
    {
        var corte = _dbContext.RevocacionesSesion
            .Where(r => r.UsuarioId == usuarioId && r.FechaExpiracion > DateTime.UtcNow)
            .Select(r => (DateTime?)r.FechaCorte)
            .FirstOrDefault();

        if (corte == null)
        {
            return true;
        }

        return emitidoEn.UtcDateTime >= corte.Value;
    }

    private void LimpiarExpirados(DateTime ahora)
    {
        _dbContext.TokensRevocados.Where(t => t.FechaExpiracion <= ahora).ExecuteDelete();
        _dbContext.RevocacionesSesion.Where(r => r.FechaExpiracion <= ahora).ExecuteDelete();
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static DateTime TruncarASegundos(DateTime fecha) =>
        new(fecha.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private int LeerMinutosExpiracion()
    {
        var minutos = _configuration.GetValue<int>($"{JwtSettings.Seccion}:MinutosExpiracion");
        return minutos > 0 ? minutos : 60;
    }
}
