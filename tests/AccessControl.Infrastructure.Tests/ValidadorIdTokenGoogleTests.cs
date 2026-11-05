using AccessControl.Domain.Excepciones;
using AccessControl.Infrastructure.Security;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControl.Infrastructure.Tests;

/// <summary>
/// Traducción de lo que responde la librería de Google (sin salir a la red): audiencia exigida, identidad devuelta y errores.
/// La firma, el emisor y el vencimiento los comprueba la librería oficial con las claves públicas de Google.
/// </summary>
public class ValidadorIdTokenGoogleTests
{
    private const string ClientId = "123456-abc.apps.googleusercontent.com";

    private static IConfiguration Config(string? clientId) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Google:ClientId"] = clientId })
            .Build();

    private static ValidadorIdTokenGoogle Validador(
        string? clientId,
        Func<string, GoogleJsonWebSignature.ValidationSettings, Task<GoogleJsonWebSignature.Payload>> firma) =>
        new(Config(clientId), NullLogger<ValidadorIdTokenGoogle>.Instance, firma);

    private static Task<GoogleJsonWebSignature.Payload> Nunca(string _, GoogleJsonWebSignature.ValidationSettings __) =>
        throw new InvalidOperationException("No debía llamarse a Google.");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RD_10_SinGoogleClientIdResponde503ControladoSinLlamarAGoogle(string? clientId)
    {
        var validador = Validador(clientId, Nunca);

        var error = await Assert.ThrowsAsync<ProveedorExternoNoDisponibleException>(() => validador.ValidarAsync("id-token"));

        Assert.Equal("proveedor_no_disponible", error.Codigo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RD_07_UnTokenVacioSeRechazaSinLlamarAGoogle(string token)
    {
        await Assert.ThrowsAsync<TokenExternoInvalidoException>(() => Validador(ClientId, Nunca).ValidarAsync(token));
    }

    [Fact]
    public async Task RF_CA_03_SeExigeQueElTokenHayaSidoEmitidoParaNuestraAplicacion()
    {
        GoogleJsonWebSignature.ValidationSettings? recibidos = null;
        var validador = Validador($"  {ClientId}  ", (_, ajustes) =>
        {
            recibidos = ajustes;
            return Task.FromResult(new GoogleJsonWebSignature.Payload { Subject = "s", Email = "a@b.com", EmailVerified = true });
        });

        await validador.ValidarAsync("id-token");

        Assert.Equal([ClientId], recibidos!.Audience);   // otra audiencia = un token emitido para otra aplicación
        Assert.True(recibidos.ExpirationTimeClockTolerance <= TimeSpan.FromMinutes(1));
        Assert.True(recibidos.IssuedAtClockTolerance <= TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RF_CA_03_DevuelveLaIdentidadQueGoogleAfirma()
    {
        var validador = Validador(ClientId, (token, _) =>
        {
            Assert.Equal("id-token", token);
            return Task.FromResult(new GoogleJsonWebSignature.Payload
            {
                Subject = "1234567890",
                Email = "persona@ejemplo.com",
                EmailVerified = true,
                Name = "Persona de Google"
            });
        });

        var identidad = await validador.ValidarAsync("id-token");

        Assert.Equal("1234567890", identidad.Subject);
        Assert.Equal("persona@ejemplo.com", identidad.Correo);
        Assert.True(identidad.CorreoVerificado);
        Assert.Equal("Persona de Google", identidad.Nombre);
    }

    [Fact]
    public async Task RF_CA_01_ConservaElDatoDeQueElCorreoNoEstaVerificado_ParaQueElServicioLoRechace()
    {
        var validador = Validador(ClientId, (_, _) =>
            Task.FromResult(new GoogleJsonWebSignature.Payload { Subject = "s", Email = "a@b.com", EmailVerified = false }));

        var identidad = await validador.ValidarAsync("id-token");

        Assert.False(identidad.CorreoVerificado);
    }

    [Fact]
    public async Task RF_CA_03_UnTokenQueLaLibreriaRechazaEsUn401SinDetalles()
    {
        var validador = Validador(ClientId, (_, _) => throw new InvalidJwtException("JWT has expired. Secreto-interno"));

        var error = await Assert.ThrowsAsync<TokenExternoInvalidoException>(() => validador.ValidarAsync("id-token"));

        Assert.DoesNotContain("expired", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Secreto-interno", error.Message);
        Assert.Equal("token_externo_invalido", error.Codigo);
    }

    [Theory]
    [InlineData(typeof(HttpRequestException))]
    [InlineData(typeof(TaskCanceledException))]
    public async Task RF_CA_03_SiNoSePuedeConsultarAGoogleEsUn503_NoUnTokenInvalido(Type tipo)
    {
        var validador = Validador(ClientId, (_, _) => throw (Exception)Activator.CreateInstance(tipo)!);

        var error = await Assert.ThrowsAsync<ProveedorExternoNoDisponibleException>(() => validador.ValidarAsync("id-token"));

        Assert.IsType(tipo, error.InnerException);
    }

    [Fact]
    public async Task RD_08_UnErrorInesperadoNoSeDisfrazaDeTokenInvalido()
    {
        var validador = Validador(ClientId, (_, _) => throw new InvalidOperationException("fallo interno"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => validador.ValidarAsync("id-token"));
    }
}
