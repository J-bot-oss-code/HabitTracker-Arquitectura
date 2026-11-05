namespace WebApi.Extensions;

/// <summary>
/// CORS para que el frontend (otro origen) pueda llamar a la API. Solo se permiten los orígenes de Cors:OrigenesPermitidos,
/// nunca un comodín: la política usa credenciales (cookie del refresh token) y con "cualquier origen" cualquier página
/// de internet podría hacer peticiones en nombre del usuario (RD-06). Sin orígenes configurados no se permite ninguno.
/// </summary>
public static class CorsExtensions
{
    public const string PoliticaFrontend = "frontend";

    public static string[] LeerOrigenesPermitidos(IConfiguration configuracion)
    {
        var origenes = (configuracion.GetSection("Cors:OrigenesPermitidos").Get<string[]>() ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o.Trim().TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var origen in origenes)
        {
            // Un origen es solo esquema, host y puerto. Un comodín, una ruta o un esquema raro es un error de configuración (RD-07).
            if (!Uri.TryCreate(origen, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
            {
                throw new InvalidOperationException(
                    $"Cors:OrigenesPermitidos contiene un origen no válido: '{origen}'. Use el formato http(s)://host[:puerto], sin comodines ni rutas.");
            }
        }

        return origenes;
    }

    public static IServiceCollection AddCorsDelFrontend(this IServiceCollection services, IConfiguration configuracion)
    {
        var origenes = LeerOrigenesPermitidos(configuracion);
        if (origenes.Length == 0)
        {
            return services;
        }

        services.AddCors(opciones => opciones.AddPolicy(PoliticaFrontend, politica => politica
            .WithOrigins(origenes)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .WithHeaders("Content-Type", "Authorization", "X-HabitTracker-Csrf")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }

    public static IApplicationBuilder UseCorsDelFrontend(this IApplicationBuilder app, IConfiguration configuracion)
    {
        return LeerOrigenesPermitidos(configuracion).Length == 0 ? app : app.UseCors(PoliticaFrontend);
    }
}
