using System.Net;
using AccessControl.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace AccessControl.Infrastructure.Tests;

/// <summary>
/// Verificador de contraseñas filtradas con un HttpMessageHandler falso: ninguna prueba sale a la red. Comprueba el
/// k-anonimato (solo viaja el prefijo de 5 caracteres) y que una falla del servicio externo no bloquee a nadie.
/// </summary>
public sealed class VerificadorHaveIBeenPwnedTests
{
    // SHA-1("password") = 5BAA61E4C9B93F3F0682250B6CF8331B7EE68FD8 (la contraseña más filtrada del mundo).
    private const string Prefijo = "5BAA6";
    private const string Sufijo = "1E4C9B93F3F0682250B6CF8331B7EE68FD8";

    private sealed class ManejadorFalso : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _respuesta;
        public List<HttpRequestMessage> Peticiones { get; } = new();

        public ManejadorFalso(Func<HttpRequestMessage, Task<HttpResponseMessage>> respuesta) => _respuesta = respuesta;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Peticiones.Add(request);
            return _respuesta(request);
        }
    }

    private static (VerificadorHaveIBeenPwned Verificador, ManejadorFalso Manejador) Crear(
        Func<HttpRequestMessage, Task<HttpResponseMessage>> respuesta)
    {
        var manejador = new ManejadorFalso(respuesta);
        var cliente = new HttpClient(manejador) { BaseAddress = new Uri("https://api.pwnedpasswords.com/") };
        return (new VerificadorHaveIBeenPwned(cliente, NullLogger<VerificadorHaveIBeenPwned>.Instance), manejador);
    }

    private static Task<HttpResponseMessage> Cuerpo(string texto, HttpStatusCode estado = HttpStatusCode.OK) =>
        Task.FromResult(new HttpResponseMessage(estado) { Content = new StringContent(texto) });

    [Fact]
    public async Task RF_CA_14_SiElSufijoApareceConCantidadMayorACero_LaClaveEstaFiltrada()
    {
        var (verificador, _) = Crear(_ => Cuerpo($"0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n{Sufijo}:9545824\r\n011053FD0102E94D6AE2F8B83D76FAF94F6:2"));

        Assert.True(await verificador.EstaFiltradaAsync("password"));
    }

    [Fact]
    public async Task RF_CA_14_SiElSufijoNoApareceEnLaLista_LaClaveNoEstaFiltrada()
    {
        var (verificador, _) = Crear(_ => Cuerpo("0018A45C4D1DEF81644B54AB7F969B88D65:1\r\n011053FD0102E94D6AE2F8B83D76FAF94F6:2"));

        Assert.False(await verificador.EstaFiltradaAsync("password"));
    }

    [Fact]
    public async Task RF_CA_14_LasLineasDeRelleno_ConCantidadCero_NoCuentanComoFiltracion()
    {
        // Con Add-Padding la API agrega sufijos falsos con cantidad 0; si uno coincidiera por casualidad no es una filtración.
        var (verificador, _) = Crear(_ => Cuerpo($"{Sufijo}:0\r\n011053FD0102E94D6AE2F8B83D76FAF94F6:2"));

        Assert.False(await verificador.EstaFiltradaAsync("password"));
    }

    [Fact]
    public async Task RD_10_SoloViajaElPrefijoDeCincoCaracteres_NuncaLaClaveNiElHashCompleto()
    {
        var (verificador, manejador) = Crear(_ => Cuerpo(""));

        await verificador.EstaFiltradaAsync("password");

        var peticion = Assert.Single(manejador.Peticiones);
        Assert.Equal(HttpMethod.Get, peticion.Method);
        Assert.Equal($"https://api.pwnedpasswords.com/range/{Prefijo}", peticion.RequestUri!.ToString());
        // Se revisa lo que viaja (ruta, consulta, cabeceras y cuerpo), sin el dominio: "pwnedpasswords" contiene la palabra "password".
        var todo = peticion.RequestUri.AbsolutePath + peticion.RequestUri.Query + peticion.Headers + peticion.Content;
        Assert.DoesNotContain("password", todo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Sufijo, todo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RF_CA_14_ElSufijoSeCompararSinImportarMayusculasYMinusculas()
    {
        var (verificador, _) = Crear(_ => Cuerpo($"{Sufijo.ToLowerInvariant()}:12"));

        Assert.True(await verificador.EstaFiltradaAsync("password"));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task RF_CA_14_SiLaApiRespondeConError_LaClaveSePermite(HttpStatusCode estado)
    {
        var (verificador, _) = Crear(_ => Cuerpo(Sufijo + ":1", estado));

        // Aunque el cuerpo "contuviera" la clave, una respuesta de error no se interpreta: fail-open.
        Assert.False(await verificador.EstaFiltradaAsync("password"));
    }

    [Fact]
    public async Task RF_CA_14_SiHayUnaFallaDeRed_LaClaveSePermite()
    {
        var (verificador, _) = Crear(_ => throw new HttpRequestException("sin conexión"));

        Assert.False(await verificador.EstaFiltradaAsync("password"));
    }

    [Fact]
    public async Task RF_CA_14_SiSeAgotaElTiempoDeEspera_LaClaveSePermite()
    {
        var (verificador, _) = Crear(_ => throw new TaskCanceledException("timeout simulado"));

        Assert.False(await verificador.EstaFiltradaAsync("password"));
    }

    [Fact]
    public async Task RD_07_SiQuienLlamaCancela_LaCancelacionSePropagaYNoSeTragaComoFalla()
    {
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        var (verificador, _) = Crear(_ => throw new TaskCanceledException("cancelada"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verificador.EstaFiltradaAsync("password", cancelacion.Token));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task RD_07_UnaClaveVacia_NoLlegaAConsultarLaApi(string? password)
    {
        var (verificador, manejador) = Crear(_ => Cuerpo(""));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => verificador.EstaFiltradaAsync(password!));

        Assert.Empty(manejador.Peticiones);
    }

    [Theory]
    [InlineData("sufijo-sin-separador", false)]
    [InlineData(":5", false)]
    [InlineData("ABC:xyz", false)]
    [InlineData("ABC:5", true)]
    [InlineData("abc:5\r\notra:1", true)]
    [InlineData("", false)]
    public void RD_07_ElAnalisisDeLaRespuestaToleraLineasMalFormadas(string cuerpo, bool esperado)
    {
        Assert.Equal(esperado, VerificadorHaveIBeenPwned.ContieneSufijo(cuerpo, "ABC"));
    }
}
