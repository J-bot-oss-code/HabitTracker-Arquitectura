using HabitTracker.Application.Notificaciones;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HabitTracker.Infrastructure.Workers;

/// <summary>
/// Proceso en segundo plano de los recordatorios por correo (RF-NEG-07): cada <c>Recordatorios:IntervaloMinutos</c> hace una
/// pasada. La lógica vive en <see cref="IRecordatoriosService"/>, que se prueba sin este proceso (RD-12); aquí solo está el ciclo
/// y su tolerancia a fallos.
/// </summary>
public sealed class RecordatoriosWorker(
    IServiceScopeFactory scopeFactory, OpcionesRecordatorios opciones, TimeProvider reloj, ILatidoRecordatorios latido,
    ILogger<RecordatoriosWorker> logger)
    : BackgroundService
{
    // Espera inicial: da tiempo a que las migraciones del arranque terminen antes de la primera pasada.
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(opciones.IntervaloMinutos);
        logger.LogInformation("RecordatoriosWorker iniciado. Intervalo: {Minutos} minutos.", opciones.IntervaloMinutos);
        try
        {
            await Task.Delay(EsperaInicial < intervalo ? EsperaInicial : intervalo, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await EjecutarUnaPasadaAsync(stoppingToken);
                await Task.Delay(intervalo, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Apagado ordenado de la aplicación.
        }

        logger.LogInformation("RecordatoriosWorker detenido.");
    }

    private async Task EjecutarUnaPasadaAsync(CancellationToken cancelacion)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<IRecordatoriosService>();
            var enviados = await servicio.EjecutarAsync(reloj.GetUtcNow().UtcDateTime, cancelacion);
            latido.PasadaCorrecta(reloj.GetUtcNow().UtcDateTime); // solo si la pasada terminó sin error: /health lo mira
            if (enviados > 0)
            {
                logger.LogInformation("Recordatorios encolados en esta pasada: {Enviados}.", enviados);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un fallo (por ejemplo, la base de datos no responde) no detiene el proceso: se reintenta en la siguiente pasada.
            logger.LogError(ex, "Error en la pasada de recordatorios.");
        }
    }
}
