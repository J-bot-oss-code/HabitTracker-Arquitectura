using AccessControl.Infrastructure.Persistence;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebApi.Extensions;

/// <summary>
/// <c>GET /health</c> para el healthcheck del contenedor: 200 si la API responde y puede abrir la base de datos, 503 si no.
/// Es anónimo y no da detalles (solo «Healthy» o «Unhealthy»): no filtra nombres de servidores ni errores (RD-08).
/// Los cuatro módulos comparten la misma base de datos, así que basta comprobar un contexto.
/// </summary>
public static class SaludDelServicioExtensions
{
    public const string Ruta = "/health";

    public static IServiceCollection AddSaludDelServicio(this IServiceCollection services)
    {
        services.AddHealthChecks().AddCheck<BaseDeDatosDisponible>("base-de-datos");
        return services;
    }

    public static IEndpointConventionBuilder MapSaludDelServicio(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHealthChecks(Ruta, new() { ResponseWriter = (http, informe) => http.Response.WriteAsync(informe.Status.ToString()) })
            .AllowAnonymous();

    internal sealed class BaseDeDatosDisponible(ApplicationDbContext contexto, HabitTrackerDbContext negocio) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                // Dos contextos: uno de Control de Acceso y uno del negocio, por si una cadena de conexión se separa en el futuro.
                var disponible = await contexto.Database.CanConnectAsync(cancellationToken)
                    && await negocio.Database.CanConnectAsync(cancellationToken);
                return disponible ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                return HealthCheckResult.Unhealthy();
            }
        }
    }
}
