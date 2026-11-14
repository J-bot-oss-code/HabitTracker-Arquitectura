using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace WebApi.Extensions;

/// <summary>
/// Límite de peticiones por IP en los endpoints anónimos que se pueden abusar (RD-06):
/// - activacion: el código de activación tiene solo 6 dígitos;
/// - login: sin límite, cualquiera podía bloquear 15 minutos la cuenta de otra persona o probar claves sin fin;
/// - registro: evita crear cuentas en masa y vaciar la cola de correos;
/// - recuperacion: evita probar códigos de recuperación e inundar de correos a una víctima;
/// - refresh: evita probar refresh tokens y cargar la base de datos con renovaciones;
/// - google: evita probar contraseñas con /google/vincular y cargar el validador de tokens;
/// - privacidad: evita probar contraseñas con la baja de cuenta y repetir la descarga de datos;
/// - catalogo: el catálogo público de planes es anónimo; el tope evita que un cliente lo descargue en bucle;
/// - usuario y subidas: los endpoints con sesión se limitan por persona (no por IP: detrás de un proxy todas comparten la
///   IP); subidas protege el disco con un tope menor para documentos.
/// Los topes son por minuto y se pueden cambiar en RateLimit:* (la configuración inválida detiene el arranque).
/// </summary>
public static class LimitacionDePeticionesExtensions
{
    public const string Activacion = "activacion";
    public const string Login = "login";
    public const string Registro = "registro";
    public const string Recuperacion = "recuperacion";
    public const string Refresh = "refresh";
    public const string Google = "google";
    public const string Privacidad = "privacidad";
    public const string Catalogo = "catalogo";
    public const string Usuario = "usuario";
    public const string Subidas = "subidas";

    public static IServiceCollection AddLimitacionDePeticiones(this IServiceCollection services, IConfiguration configuracion)
    {
        var limites = new (string Politica, string Clave, int PorDefecto)[]
        {
            (Activacion, "ActivacionPorMinuto", 30),
            (Login, "LoginPorMinuto", 10),
            (Registro, "RegistroPorMinuto", 5),
            (Recuperacion, "RecuperacionPorMinuto", 5),
            (Refresh, "RefreshPorMinuto", 30),
            (Google, "GooglePorMinuto", 10),
            (Privacidad, "PrivacidadPorMinuto", 10),
            (Catalogo, "CatalogoPorMinuto", 60),
            (Usuario, "UsuarioPorMinuto", 120),
            (Subidas, "SubidasPorMinuto", 10),
        };

        services.AddRateLimiter(opciones =>
        {
            opciones.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            foreach (var (politica, clave, porDefecto) in limites)
            {
                var permitidas = LeerLimite(configuracion, clave, porDefecto);
                var porPersona = politica is Usuario or Subidas;
                opciones.AddPolicy(politica, contexto =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        porPersona ? ClaveDePersona(contexto) : ClaveDeIp(contexto),
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

    private static string ClaveDeIp(HttpContext contexto) => contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocida";

    /// <summary>La persona de la sesión (el limitador corre después de la autenticación); sin sesión, la IP.</summary>
    public static string ClaveDePersona(HttpContext contexto) =>
        contexto.User.FindFirstValue(ClaimTypes.NameIdentifier) is { Length: > 0 } id ? $"u:{id}" : ClaveDeIp(contexto);

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
