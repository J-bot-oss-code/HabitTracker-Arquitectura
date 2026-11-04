using System.Net;
using System.Text;
using HabitTracker.Application.Exceptions;
using HabitTracker.Infrastructure.Clima;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HabitTracker.Integration.Tests;

public class OpenWeatherServicioClimaTests
{
    private const string ClaveSecreta = "clave-super-secreta-12345";

    private const string RespuestaLluvia =
        """{"weather":[{"id":501,"main":"Rain","description":"lluvia moderada"}],"main":{"temp":24.5},"name":"Santo Domingo","cod":200}""";

    private const string RespuestaDespejado =
        """{"weather":[{"id":800,"main":"Clear","description":"cielo claro"}],"main":{"temp":31.2},"name":"Santo Domingo","cod":200}""";

    private static (OpenWeatherServicioClima Servicio, ManejadorFalso Manejador, RegistroDeLogs Logs) Crear(
        ManejadorFalso manejador, string? clave = ClaveSecreta)
    {
        var http = new HttpClient(manejador) { BaseAddress = new Uri("https://api.openweathermap.org/") };
        var logs = new RegistroDeLogs();
        var servicio = new OpenWeatherServicioClima(
            http, Options.Create(new OpenWeatherOptions { ApiKey = clave }), logs);
        return (servicio, manejador, logs);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_RespuestaCorrecta_SeConvierteEnLaCondicionClimatica()
    {
        var (servicio, _, _) = Crear(ManejadorFalso.Responde(HttpStatusCode.OK, RespuestaDespejado));

        var clima = await servicio.ObtenerAsync("Santo Domingo");

        Assert.Equal(31.2m, clima.TemperaturaC);
        Assert.False(clima.Precipitacion);
        Assert.Equal("cielo claro", clima.Descripcion);
    }

    [Theory]
    [InlineData(200, true)]  // 2xx tormenta
    [InlineData(211, true)]
    [InlineData(300, true)]  // 3xx llovizna
    [InlineData(501, true)]  // 5xx lluvia
    [InlineData(601, true)]  // 6xx nieve
    [InlineData(701, false)] // 7xx bruma, niebla...
    [InlineData(800, false)] // despejado
    [InlineData(804, false)] // nublado
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_CodigoDeClima_DeterminaSiHayPrecipitacion(int codigo, bool esperado)
    {
        var json = $$$"""{"weather":[{"id":{{{codigo}}},"description":"x"}],"main":{"temp":20}}""";
        var (servicio, _, _) = Crear(ManejadorFalso.Responde(HttpStatusCode.OK, json));

        var clima = await servicio.ObtenerAsync("Santo Domingo");

        Assert.Equal(esperado, clima.Precipitacion);
    }

    [Fact]
    [Trait("Requirement", "RF-NEG-02")]
    public async Task RF_NEG_02_LaPeticion_LlevaLaCiudadCodificadaLaClaveYUnidadesMetricas()
    {
        var (servicio, manejador, _) = Crear(ManejadorFalso.Responde(HttpStatusCode.OK, RespuestaLluvia));

        await servicio.ObtenerAsync("São Paulo & más");

        var url = manejador.UltimaUrl!;
        Assert.Equal("api.openweathermap.org", url.Host);
        Assert.Contains("units=metric", url.Query);
        Assert.Contains("appid=" + ClaveSecreta, url.Query);
        Assert.DoesNotContain("& más", url.Query); // el "&" de la ciudad no puede inyectar parámetros
        Assert.Contains("%26", url.Query);
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_DescripcionDemasiadoLarga_SeRecorta()
    {
        var larga = new string('x', 5000);
        var json = $$$"""{"weather":[{"id":800,"description":"{{{larga}}}"}],"main":{"temp":20}}""";
        var (servicio, _, _) = Crear(ManejadorFalso.Responde(HttpStatusCode.OK, json));

        var clima = await servicio.ObtenerAsync("Santo Domingo");

        Assert.Equal(200, clima.Descripcion.Length);
    }

    [Fact]
    [Trait("Requirement", "RD-10")]
    public async Task RD_10_SinClave_RechazaSinLlamarAlProveedor()
    {
        var (servicio, manejador, _) = Crear(ManejadorFalso.Responde(HttpStatusCode.OK, RespuestaDespejado), clave: null);

        await Assert.ThrowsAsync<ServicioClimaNoDisponibleException>(() => servicio.ObtenerAsync("Santo Domingo"));

        Assert.Equal(0, manejador.Llamadas);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Requirement", "RD-10")]
    public async Task RD_10_ClaveVaciaOEnBlanco_RechazaSinLlamarAlProveedor(string clave)
    {
        var (servicio, manejador, _) = Crear(ManejadorFalso.Responde(HttpStatusCode.OK, RespuestaDespejado), clave);

        await Assert.ThrowsAsync<ServicioClimaNoDisponibleException>(() => servicio.ObtenerAsync("Santo Domingo"));

        Assert.Equal(0, manejador.Llamadas);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_ErrorDelProveedor_SeConvierteEnNoDisponible(HttpStatusCode codigo)
    {
        var (servicio, _, _) = Crear(ManejadorFalso.Responde(codigo, """{"cod":401,"message":"Invalid API key"}"""));

        await Assert.ThrowsAsync<ServicioClimaNoDisponibleException>(() => servicio.ObtenerAsync("Santo Domingo"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_CiudadInexistente_DaCiudadNoEncontrada()
    {
        var (servicio, _, _) = Crear(ManejadorFalso.Responde(HttpStatusCode.NotFound, """{"cod":"404","message":"city not found"}"""));

        await Assert.ThrowsAsync<CiudadNoEncontradaException>(() => servicio.ObtenerAsync("Narnia"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_ProveedorCaido_ErrorDeRed_SeConvierteEnNoDisponible()
    {
        var (servicio, _, _) = Crear(ManejadorFalso.Lanza(new HttpRequestException("No es posible conectar con el servidor remoto")));

        await Assert.ThrowsAsync<ServicioClimaNoDisponibleException>(() => servicio.ObtenerAsync("Santo Domingo"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_Timeout_SeConvierteEnNoDisponible()
    {
        var (servicio, _, _) = Crear(ManejadorFalso.Lanza(new TaskCanceledException("timeout")));

        await Assert.ThrowsAsync<ServicioClimaNoDisponibleException>(() => servicio.ObtenerAsync("Santo Domingo"));
    }

    [Fact]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_CancelacionDelCliente_NoSeDisfrazaDeFalloDelProveedor()
    {
        using var cancelacion = new CancellationTokenSource();
        await cancelacion.CancelAsync();
        var (servicio, _, _) = Crear(ManejadorFalso.Lanza(new TaskCanceledException()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => servicio.ObtenerAsync("Santo Domingo", cancelacion.Token));
    }

    [Theory]
    [InlineData("esto no es json")]
    [InlineData("{}")]
    [InlineData("""{"weather":[],"main":{"temp":20}}""")]
    [InlineData("""{"weather":[{"id":800,"description":"x"}],"main":{}}""")]
    [InlineData("""{"weather":[{"id":"ochocientos","description":"x"}],"main":{"temp":20}}""")]
    [InlineData("""{"weather":[{"id":800,"description":"x"}],"main":{"temp":"calor"}}""")]
    [Trait("Requirement", "RD-07")]
    public async Task RD_07_RespuestaConFormatoInesperado_SeConvierteEnNoDisponible(string cuerpo)
    {
        var (servicio, _, _) = Crear(ManejadorFalso.Responde(HttpStatusCode.OK, cuerpo));

        await Assert.ThrowsAsync<ServicioClimaNoDisponibleException>(() => servicio.ObtenerAsync("Santo Domingo"));
    }

    [Fact]
    [Trait("Requirement", "RD-10")]
    public async Task RD_10_LaClaveNuncaApareceEnLosMensajesNiEnLosLogs()
    {
        var escenarios = new[]
        {
            ManejadorFalso.Responde(HttpStatusCode.Unauthorized, "{}"),
            ManejadorFalso.Responde(HttpStatusCode.OK, "no es json"),
            ManejadorFalso.Lanza(new HttpRequestException($"fallo en https://api.openweathermap.org/?appid={ClaveSecreta}")),
            ManejadorFalso.Lanza(new TaskCanceledException($"timeout en ...appid={ClaveSecreta}"))
        };

        foreach (var manejador in escenarios)
        {
            var (servicio, _, logs) = Crear(manejador);

            var ex = await Assert.ThrowsAsync<ServicioClimaNoDisponibleException>(() => servicio.ObtenerAsync("Santo Domingo"));

            Assert.DoesNotContain(ClaveSecreta, ex.Message);
            Assert.DoesNotContain(ClaveSecreta, ex.ToString());
            Assert.DoesNotContain(logs.Mensajes, m => m.Contains(ClaveSecreta));
            Assert.DoesNotContain(logs.Excepciones, e => e is not null);
        }
    }

    // ---------- Dobles ----------

    private sealed class ManejadorFalso : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _respuesta;

        private ManejadorFalso(Func<HttpResponseMessage> respuesta) => _respuesta = respuesta;

        public int Llamadas { get; private set; }
        public Uri? UltimaUrl { get; private set; }

        public static ManejadorFalso Responde(HttpStatusCode codigo, string cuerpo) =>
            new(() => new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") });

        public static ManejadorFalso Lanza(Exception excepcion) =>
            new(() => throw excepcion);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Llamadas++;
            UltimaUrl = request.RequestUri;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_respuesta());
        }
    }

    private sealed class RegistroDeLogs : ILogger<OpenWeatherServicioClima>
    {
        public List<string> Mensajes { get; } = [];
        public List<Exception?> Excepciones { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Mensajes.Add(formatter(state, exception));
            Excepciones.Add(exception);
        }
    }
}
