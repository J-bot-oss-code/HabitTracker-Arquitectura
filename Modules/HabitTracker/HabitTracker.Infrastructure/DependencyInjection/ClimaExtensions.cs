using HabitTracker.Application.Interfaces;
using HabitTracker.Infrastructure.Clima;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HabitTracker.Infrastructure.DependencyInjection;

public static class ClimaExtensions
{
    public static IServiceCollection AddServicioClimaOpenWeather(this IServiceCollection services, IConfiguration configuration)
    {
        // La clave se lee de la variable de entorno (RD-10); si falta, la operación de clima se rechaza de forma controlada.
        services.Configure<OpenWeatherOptions>(opciones => opciones.ApiKey = configuration["OPENWEATHER_API_KEY"]);

        services.AddHttpClient<IServicioClima, OpenWeatherServicioClima>(cliente =>
        {
            cliente.BaseAddress = new Uri("https://api.openweathermap.org/");
            // Un proveedor lento no debe dejar colgada la petición del usuario.
            cliente.Timeout = TimeSpan.FromSeconds(5);
            // La respuesta real pesa menos de 2 KB: un proveedor comprometido no puede obligarnos a bufferizar gigas.
            cliente.MaxResponseContentBufferSize = 64 * 1024;
        })
        // La clave viaja en la URL: sin redirecciones nunca llegaría a otro host.
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
        // El registro automático de HttpClient escribe la URL completa (con appid) si alguien baja el nivel de log.
        .RemoveAllLoggers();

        return services;
    }
}
