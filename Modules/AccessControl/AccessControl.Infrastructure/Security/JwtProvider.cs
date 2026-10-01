using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace AccessControl.Infrastructure.Security;

public class JwtProvider : IJwtProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtProvider> _logger;

    public JwtProvider(IConfiguration configuration, ILogger<JwtProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string Generar(Usuario usuario)
    {
        var settings = LeerSettings();

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, usuario.Correo.Valor),
            new Claim(ClaimTypes.Role, usuario.Rol.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Clave));
        var credenciales = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expira = DateTime.UtcNow.AddMinutes(settings.MinutosExpiracion);

        var token = new JwtSecurityToken(
            issuer: settings.Emisor,
            audience: settings.Audiencia,
            claims: claims,
            expires: expira,
            signingCredentials: credenciales);

        _logger.LogInformation("Token JWT generado para el usuario {UsuarioId}.", usuario.Id);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private JwtSettings LeerSettings()
    {
        var settings = new JwtSettings();
        _configuration.GetSection(JwtSettings.Seccion).Bind(settings);

        if (string.IsNullOrWhiteSpace(settings.Clave) || Encoding.UTF8.GetBytes(settings.Clave).Length < 32)
        {
            throw new InvalidOperationException("Falta configuración JWT válida (JwtSettings:Clave de al menos 32 caracteres).");
        }

        if (string.IsNullOrWhiteSpace(settings.Emisor) || string.IsNullOrWhiteSpace(settings.Audiencia))
        {
            throw new InvalidOperationException("Falta configuración JWT válida (JwtSettings:Emisor y JwtSettings:Audiencia).");
        }

        if (settings.MinutosExpiracion <= 0)
        {
            throw new InvalidOperationException("La configuración JWT no es válida (JwtSettings:MinutosExpiracion debe ser mayor a cero).");
        }

        return settings;
    }
}
