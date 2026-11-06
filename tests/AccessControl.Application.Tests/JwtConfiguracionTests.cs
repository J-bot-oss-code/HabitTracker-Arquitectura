using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControl.Application.Tests;

/// <summary>Configuración JWT: una sola validación (falla al arrancar) y el nombre del usuario en el token.</summary>
public class JwtConfiguracionTests
{
    private static IConfiguration Config(params (string Clave, string? Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.ToDictionary(v => v.Clave, v => v.Valor))
            .Build();


    [Theory]
    [InlineData("", "emisor", "audiencia", 60)]
    [InlineData("clave-demasiado-corta", "emisor", "audiencia", 60)]
    [InlineData("una-clave-de-prueba-de-al-menos-32-caracteres", "", "audiencia", 60)]
    [InlineData("una-clave-de-prueba-de-al-menos-32-caracteres", "emisor", " ", 60)]
    [InlineData("una-clave-de-prueba-de-al-menos-32-caracteres", "emisor", "audiencia", 0)]
    public void RD_10_UnaConfiguracionJwtIncompleta_NoPasaLaValidacion(string clave, string emisor, string audiencia, int minutos)
    {
        var ajustes = new JwtSettings { Clave = clave, Emisor = emisor, Audiencia = audiencia, MinutosExpiracion = minutos };

        Assert.Throws<InvalidOperationException>(ajustes.Validar);
    }

    [Fact]
    public void RD_10_UnaConfiguracionJwtCompleta_PasaLaValidacion()
    {
        new JwtSettings
        {
            Clave = "una-clave-de-prueba-de-al-menos-32-caracteres",
            Emisor = "emisor",
            Audiencia = "audiencia",
            MinutosExpiracion = 15
        }.Validar();
    }

    [Fact]
    public void RF_CA_03_ElTokenDeAccesoDuraPocoPorDefecto_LaSesionLargaLaSostieneElRefreshToken()
    {
        Assert.Equal(15, new JwtSettings().MinutosExpiracion);

        var cfg = Config(
            ("JwtSettings:Clave", "una-clave-de-prueba-de-al-menos-32-caracteres"),
            ("JwtSettings:Emisor", "emisor"),
            ("JwtSettings:Audiencia", "audiencia"));
        var usuario = new Usuario("Persona", new Email("persona@ejemplo.com"), "hash", Rol.Estandar);

        var token = new JwtProvider(cfg, NullLogger<JwtProvider>.Instance).Generar(usuario);

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.InRange(jwt.ValidTo, DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
    }

    [Fact]
    public void RF_CA_07_ElTokenIncluyeElNombreDelUsuarioParaGetMe()
    {
        var cfg = Config(
            ("JwtSettings:Clave", "una-clave-de-prueba-de-al-menos-32-caracteres"),
            ("JwtSettings:Emisor", "emisor"),
            ("JwtSettings:Audiencia", "audiencia"),
            ("JwtSettings:MinutosExpiracion", "15"));
        var usuario = new Usuario("Persona de Prueba", new Email("persona@ejemplo.com"), "hash", Rol.Estandar);

        var token = new JwtProvider(cfg, NullLogger<JwtProvider>.Instance).Generar(usuario);

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("Persona de Prueba", jwt.Claims.Single(c => c.Type == "name").Value);
    }
}
