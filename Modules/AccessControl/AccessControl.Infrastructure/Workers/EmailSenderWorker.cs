using AccessControl.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Workers;

/// <summary>Proceso independiente que vacía la cola de correos cada 15 segundos (RF-NOT-09).</summary>
public class EmailSenderWorker(IServiceScopeFactory scopeFactory, ILogger<EmailSenderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EmailSenderWorker iniciado. Intervalo: 15 segundos.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ProcesadorColaCorreos>().ProcesarAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Error inesperado en el ciclo del EmailSenderWorker.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("EmailSenderWorker detenido.");
    }
}
