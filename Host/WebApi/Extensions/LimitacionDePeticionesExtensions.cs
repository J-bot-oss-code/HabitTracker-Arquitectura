using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace WebApi.Extensions;

/// <summary>
/// Límite de peticiones por IP en los endpoints anónimos que se pueden abusar (RD-06):
/// - activacion: el código de activación tiene solo 6 dígitos;
/// - login: sin límite, cualquiera podía bloquear 15 minutos la cuenta de otra persona o probar claves sin fin;
/// - registro: evita crear cuentas en masa y vaciar la cola de correos;
/// - recuperacion: evita probar códigos de recuperación e inundar de correos a una víctima.
/// Los topes son por minuto y se pueden cambiar en RateLimit:* (la configuración inválida detiene el arranque).
/// </summary>
public static class LimitacionDePeticionesExtensions
{
    public const string Activacion = "activacion";
    public const string Login = "login";
    public const string Registro = "registro";
    public const string Recuperacion = "recuperacion";

    public static IServiceCollection AddLimitacionDePeticiones(this IServiceCollection services, IConfiguration configuracion)
    {
        var limites = new (string Politica, string Clave, int PorDefecto)[]
        {
            (Activacion, "ActivacionPorMinuto", 30),
            (Login, "LoginPorMinuto", 10),
            (Registro, "RegistroPorMinuto", 5),
            (Recuperacion, "RecuperacionPorMinuto", 5),
        };

        services.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            foreach (var (politica, clave, porDefecto) in limites)
            {
                var permitidas = LeerLimite(configuracion, clave, porDefecto);
                opciones.AddPolicy(politica, contexto =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitidas,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        }));
            }

            opciones.OnRejected = async (contexto, cancelacion) =>
            {
                // Mismo formato de error que el resto de la API (RFC 7807) y sin detalles internos (RD-08).
                contexto.HttpContext.Response.ContentType = "application/problem+json";
                await contexto.HttpContext.Response.WriteAsync(
                    "{\"title\":\"Demasiadas solicitudes.\",\"status\":429,\"detail\":\"Intente de nuevo en un minuto.\"}",
                    cancelacion);
            };
        });

        return services;
    }

    private static int LeerLimite(IConfiguration configuracion, string clave, int porDefecto)
    {
        var valor = configuracion.GetValue<int?>($"RateLimit:{clave}") ?? porDefecto;
        if (valor <= 0)
        {
            // Un tope de 0 (o negativo) bloquearía el endpoint para todo el mundo: es un error de configuración.
            throw new InvalidOperationException($"La configuración RateLimit:{clave} debe ser un entero mayor que cero.");
        }

        return valor;
    }
}
