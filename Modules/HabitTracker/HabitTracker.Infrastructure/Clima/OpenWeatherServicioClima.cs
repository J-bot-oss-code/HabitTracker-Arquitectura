using System.Text.Json;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Servicios;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HabitTracker.Infrastructure.Clima;

public sealed class OpenWeatherOptions
{
    /// <summary>Clave de la API. Llega de la variable de entorno OPENWEATHER_API_KEY (RD-10); nunca está en el código.</summary>
    public string? ApiKey { get; set; }
}

/// <summary>
/// Adaptador del puerto <see cref="IServicioClima"/> para OpenWeatherMap (https://openweathermap.org/current).
/// Regla de oro: pase lo que pase, solo salen de aquí dos excepciones controladas y sus mensajes son genéricos.
/// La clave viaja en la URL (así lo exige el proveedor), por eso NUNCA se registra una URL ni una excepción de red.
/// </summary>
public sealed class OpenWeatherServicioClima(
    HttpClient http,
    IOptions<OpenWeatherOptions> opciones,
    ILogger<OpenWeatherServicioClima> logger) : IServicioClima
{
    private const int LongitudMaximaDescripcion = 200;

    public async Task<CondicionClimatica> ObtenerAsync(string ciudad, CancellationToken cancellationToken = default)
    {
        var clave = opciones.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(clave))
        {
            // Sin clave no se intenta la llamada: el resto del proyecto sigue funcionando y la operación se rechaza.
            logger.LogWarning("OPENWEATHER_API_KEY no está definida: el servicio de clima no está disponible.");
            throw new ServicioClimaNoDisponibleException();
        }

        var ruta = $"data/2.5/weather?q={Uri.EscapeDataString(ciudad)}&units=metric&lang=es&appid={Uri.EscapeDataString(clave)}";

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.GetAsync(ruta, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // El cliente canceló: no es un fallo del proveedor.
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            // Se registra solo el TIPO del error, no la excepción: su texto podría arrastrar la URL con la clave.
            logger.LogWarning("No se pudo contactar al proveedor de clima ({Tipo}).", ex.GetType().Name);
            throw new ServicioClimaNoDisponibleException();
        }

        using (respuesta)
        {
            if (respuesta.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new CiudadNoEncontradaException();
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                // 401 clave inválida o no activada, 429 límite de uso, 5xx caída del proveedor...
                logger.LogWarning("El proveedor de clima respondió {Codigo}.", (int)respuesta.StatusCode);
                throw new ServicioClimaNoDisponibleException();
            }

            return await LeerAsync(respuesta, cancellationToken);
        }
    }

    private async Task<CondicionClimatica> LeerAsync(HttpResponseMessage respuesta, CancellationToken cancellationToken)
    {
        try
        {
            await using var contenido = await respuesta.Content.ReadAsStreamAsync(cancellationToken);
            using var documento = await JsonDocument.ParseAsync(contenido, cancellationToken: cancellationToken);
            var raiz = documento.RootElement;

            var temperatura = raiz.GetProperty("main").GetProperty("temp").GetDecimal();
            var clima = raiz.GetProperty("weather")[0];
            var codigo = clima.GetProperty("id").GetInt32();
            var descripcion = clima.GetProperty("description").GetString() ?? string.Empty;
            if (descripcion.Length > LongitudMaximaDescripcion)
            {
                descripcion = descripcion[..LongitudMaximaDescripcion];
            }

            // Códigos de OpenWeatherMap: 2xx tormenta, 3xx llovizna, 5xx lluvia, 6xx nieve.
            var precipitacion = codigo is >= 200 and < 700;

            return new CondicionClimatica(temperatura, precipitacion, descripcion);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException
                                       or IndexOutOfRangeException or FormatException)
        {
            logger.LogWarning("La respuesta del proveedor de clima no tiene el formato esperado ({Tipo}).", ex.GetType().Name);
            throw new ServicioClimaNoDisponibleException();
        }
    }
}
