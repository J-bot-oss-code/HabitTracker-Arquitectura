using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace WebApi.Extensions;

/// <summary>
/// Detrás de un proxy inverso, <c>RemoteIpAddress</c> es la IP del proxy y todos los clientes comparten el cupo del limitador de
/// peticiones por IP. Con <c>ProxiesConfiables</c> (lista de IP, por ejemplo <c>ProxiesConfiables__0=172.18.0.2</c>) se leen
/// <c>X-Forwarded-For</c> y <c>X-Forwarded-Proto</c> SOLO de esos proxies. Vacío (por defecto) no se confía en ninguna cabecera:
/// aceptarlas de cualquiera permitiría a un cliente falsificar su IP para esquivar los límites. Una IP inválida detiene el arranque.
/// </summary>
public static class ReenvioDeCabecerasExtensions
{
    public static IServiceCollection AddReenvioDeCabeceras(this IServiceCollection services, IConfiguration configuracion)
    {
        var proxies = LeerProxies(configuracion);
        if (proxies.Count == 0)
            return services;

        services.Configure<ForwardedHeadersOptions>(opciones =>
        {
            opciones.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            // Por defecto solo se confía en el bucle local: se sustituye por los proxies indicados.
            opciones.KnownProxies.Clear();
            opciones.KnownIPNetworks.Clear();
            foreach (var proxy in proxies)
                opciones.KnownProxies.Add(proxy);
        });
        return services;
    }

    public static IApplicationBuilder UseReenvioDeCabeceras(this IApplicationBuilder app)
    {
        var opciones = app.ApplicationServices.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        return opciones.ForwardedHeaders == ForwardedHeaders.None ? app : app.UseForwardedHeaders();
    }

    public static IReadOnlyList<IPAddress> LeerProxies(IConfiguration configuracion)
    {
        var lista = new List<IPAddress>();
        foreach (var texto in configuracion.GetSection("ProxiesConfiables").Get<string[]>() ?? [])
        {
            // El compose pasa la variable aunque esté vacía: una entrada vacía significa «sin proxy».
            if (string.IsNullOrWhiteSpace(texto))
                continue;
            if (!IPAddress.TryParse(texto?.Trim(), out var ip))
                throw new InvalidOperationException("ProxiesConfiables debe ser una lista de direcciones IP válidas.");
            lista.Add(ip);
        }
        return lista;
    }
}
