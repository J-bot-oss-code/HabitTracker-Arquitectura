using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Infrastructure.Persistence;
using AccessControl.Infrastructure.Security;
using AccessControl.Infrastructure.Services;
using AccessControl.Infrastructure.Workers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControl.Infrastructure.Tests;

/// <summary>Lista negra de tokens y cola de correos con EF Core sobre SQLite en memoria (sin la API ni SQL Server).</summary>
public sealed class ColaYBlacklistTests : IDisposable
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    public ColaYBlacklistTests()
    {
        _conexion.Open();
        using var contexto = NuevoContexto();
        contexto.Database.EnsureCreated();
    }

    public void Dispose() => _conexion.Dispose();

    private ApplicationDbContext NuevoContexto() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_conexion).Options);

    private static IConfiguration Config(int minutosJwt) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["JwtSettings:Clave"] = "una-clave-de-prueba-de-al-menos-32-caracteres",
            ["JwtSettings:Emisor"] = "emisor",
            ["JwtSettings:Audiencia"] = "audiencia",
            ["JwtSettings:MinutosExpiracion"] = minutosJwt.ToString()
        }).Build();

    // ---------- RF-CA-18: el token cerrado deja de servir hasta que venza de verdad ----------

    [Fact]
    public void RF_CA_18_LaListaNegraGuardaElTokenHastaSuVencimientoRealYNoUnPlazoFijo()
    {
        var usuario = new Usuario("Persona", new Email("persona@ejemplo.com"), "hash", Rol.Estandar);
        // El token se emite con 15 minutos de vida...
        var token = new JwtProvider(Config(15), NullLogger<JwtProvider>.Instance).Generar(usuario);

        using var contexto = NuevoContexto();
        // ...pero la configuración actual dice 60: antes la entrada vivía 60 minutos desde el cierre de sesión.
        var servicio = new TokenBlacklistService(contexto, Config(60), NullLogger<TokenBlacklistService>.Instance);

        servicio.InvalidarToken(token);

        var vencimientoDelToken = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo;
        var guardado = contexto.TokensRevocados.Single().FechaExpiracion;
        Assert.InRange((guardado - vencimientoDelToken).TotalSeconds, -2, 2);
        Assert.True(guardado < DateTime.UtcNow.AddMinutes(16));
        Assert.True(servicio.EstaInvalidado(token));
        Assert.False(servicio.EstaInvalidado(token + "x"));
    }

    [Fact]
    public void RF_CA_18_UnTokenQueNoSePuedeLeer_UsaElPlazoDeLaConfiguracion()
    {
        using var contexto = NuevoContexto();
        var servicio = new TokenBlacklistService(contexto, Config(20), NullLogger<TokenBlacklistService>.Instance);

        servicio.InvalidarToken("esto-no-es-un-jwt");

        var guardado = contexto.TokensRevocados.Single().FechaExpiracion;
        Assert.InRange((guardado - DateTime.UtcNow).TotalMinutes, 19, 21);
    }

    // ---------- RF-NOT-12 / RD-09: el worker no relee los correos que ya agotaron sus intentos ----------

    private static CorreoEnCola Correo(string destino) => new(destino, "Asunto", "<p>Cuerpo</p>");

    private static CorreoEnCola FallidoConIntentos(string destino, int intentos)
    {
        var correo = Correo(destino);
        for (var i = 1; i <= intentos; i++)
        {
            if (i > 1)
            {
                correo.Reintentar();
            }

            correo.MarcarProcesando();
            correo.RegistrarFallo("fallo simulado");
        }

        return correo;
    }

    [Fact]
    public async Task RD_09_ElWorkerSoloConsultaPendientesYFallidosConIntentosRestantes()
    {
        var enviado = Correo("enviado@ejemplo.com");
        enviado.MarcarProcesando();
        enviado.MarcarEnviado();
        var procesando = Correo("procesando@ejemplo.com");
        procesando.MarcarProcesando();

        using (var contexto = NuevoContexto())
        {
            contexto.CorreosEnCola.AddRange(
                Correo("pendiente@ejemplo.com"),
                FallidoConIntentos("fallido1@ejemplo.com", 1),
                FallidoConIntentos("fallido2@ejemplo.com", 2),
                FallidoConIntentos("agotado@ejemplo.com", CorreoEnCola.MaxIntentos),
                enviado,
                procesando);
            await contexto.SaveChangesAsync();
        }

        using var lectura = NuevoContexto();
        var candidatos = await lectura.CorreosEnCola.Where(EmailSenderWorker.CandidatosDeEnvio)
            .Select(c => c.Destinatario).OrderBy(d => d).ToListAsync();

        Assert.Equal(["fallido1@ejemplo.com", "fallido2@ejemplo.com", "pendiente@ejemplo.com"], candidatos);
    }
}
