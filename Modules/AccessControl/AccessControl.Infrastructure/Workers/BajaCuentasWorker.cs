using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Workers;

/// <summary>
/// Proceso en segundo plano que borra definitivamente las cuentas cuyo periodo de gracia terminó (una vez al día por
/// defecto, Privacidad:IntervaloEjecucionMinutos). La lógica vive en <see cref="IServicioBajaCuentas"/>, que se prueba sin
/// este proceso (RD-12); aquí solo está el ciclo y su tolerancia a fallos.
/// </summary>
public sealed class BajaCuentasWorker : BackgroundService
{
    // Espera inicial: da tiempo a que las migraciones del arranque terminen antes de la primera pasada.
    private static readonly TimeSpan EsperaInicial = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OpcionesPrivacidad _opciones;
    private readonly ILogger<BajaCuentasWorker> _logger;

    public BajaCuentasWorker(IServiceScopeFactory scopeFactory, OpcionesPrivacidad opciones, ILogger<BajaCuentasWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(_opciones.IntervaloEjecucionMinutos);
        _logger.LogInformation("BajaCuentasWorker iniciado. Intervalo: {Minutos} minutos.", _opciones.IntervaloEjecucionMinutos);

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

        _logger.LogInformation("BajaCuentasWorker detenido.");
    }

    private async Task EjecutarUnaPasadaAsync(CancellationToken cancelacion)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var servicio = scope.ServiceProvider.GetRequiredService<IServicioBajaCuentas>();
            var borradas = await servicio.EjecutarAsync(DateTime.UtcNow, cancelacion);

            if (borradas > 0)
            {
                _logger.LogInformation("Bajas de cuenta ejecutadas en esta pasada: {Borradas}.", borradas);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Un fallo (por ejemplo, la base de datos no responde) no detiene el proceso: se reintenta en la siguiente pasada.
            _logger.LogError(ex, "Error en la pasada de bajas de cuenta.");
        }
    }
}
