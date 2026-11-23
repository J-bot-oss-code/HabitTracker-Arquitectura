using AccessControl.Infrastructure.Persistence;
using HabitTracker.Application.Notificaciones;
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
        services.AddHealthChecks()
            .AddCheck<BaseDeDatosDisponible>("base-de-datos")
            // Un proceso de recordatorios parado no tumba la API: se marca «Degraded» (sigue respondiendo 200) para que un
            // monitor externo que busque la palabra «Healthy» avise, sin que el contenedor se reinicie por un fallo pasajero.
            .AddCheck<RecordatoriosVivos>("recordatorios", failureStatus: HealthStatus.Degraded);
        return services;
    }

    public static IEndpointConventionBuilder MapSaludDelServicio(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapHealthChecks(Ruta, new() { ResponseWriter = (http, informe) => http.Response.WriteAsync(informe.Status.ToString()) })
            .AllowAnonymous()
            // Es público (lo usa el monitor externo) y abre la base en cada llamada: límite por IP como el catálogo (SEG-16).
            .RequireRateLimiting(LimitacionDePeticionesExtensions.Catalogo);

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

    /// <summary>Sana si el proceso de recordatorios está apagado a propósito o si completó una pasada hace poco (sin detalles: RD-08).</summary>
    public sealed class RecordatoriosVivos(OpcionesRecordatorios opciones, ILatidoRecordatorios latido, TimeProvider? reloj = null) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!opciones.Habilitado)
                return Task.FromResult(HealthCheckResult.Healthy());

            var ahora = (reloj ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            var vivo = LatidoRecordatorios.EstaVivo(
                ahora, latido.IniciadoEnUtc, latido.UltimaPasadaCorrectaUtc, TimeSpan.FromMinutes(opciones.IntervaloMinutos));
            return Task.FromResult(vivo ? HealthCheckResult.Healthy() : HealthCheckResult.Degraded());
        }
    }
}
