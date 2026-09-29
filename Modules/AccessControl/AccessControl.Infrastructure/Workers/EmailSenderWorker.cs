using System.Net;
using System.Net.Mail;
using AccessControl.Domain.Entities;
using AccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Workers;

public class EmailSenderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailSenderWorker> _logger;

    public EmailSenderWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<EmailSenderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailSenderWorker iniciado. Intervalo: 15 segundos.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcesarColaAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error inesperado en el ciclo del EmailSenderWorker.");
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

        _logger.LogInformation("EmailSenderWorker detenido.");
    }

    private async Task ProcesarColaAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var pendientes = await dbContext.CorreosEnCola
            .Where(c => c.Estado == EstadoCorreo.Pendiente)
            .ToListAsync(stoppingToken);

        if (pendientes.Count == 0)
        {
            return;
        }

        _logger.LogInformation("EmailSenderWorker encontró {Count} correos pendientes.", pendientes.Count);

        foreach (var correo in pendientes)
        {
            // RF-NOT-12: reclamar el correo ANTES de enviarlo para evitar duplicados si el worker se reinicia.
            correo.MarcarProcesando();
            await dbContext.SaveChangesAsync(stoppingToken);

            try
            {
                await EnviarPorSmtpAsync(correo, stoppingToken);

                correo.MarcarEnviado();
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogInformation("Correo {Id} enviado a {Destino}.", correo.Id, correo.Destinatario);
            }
            catch (Exception ex)
            {
                correo.MarcarFallido();
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogWarning(ex, "Fallo el envío del correo {Id} a {Destino}. Marcado como Fallido.", correo.Id, correo.Destinatario);
            }
        }
    }

    private async Task EnviarPorSmtpAsync(CorreoEnCola correo, CancellationToken stoppingToken)
    {
        // RF-NOT-13 / RD-10: credenciales obligatoriamente desde configuración (variables de entorno).
        var host = _configuration["SMTP_HOST"];
        var portRaw = _configuration["SMTP_PORT"];
        var user = _configuration["SMTP_USER"];
        var pass = _configuration["SMTP_PASS"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(portRaw)
            || string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
        {
            throw new InvalidOperationException("Falta configuración SMTP (SMTP_HOST, SMTP_PORT, SMTP_USER, SMTP_PASS).");
        }

        if (!int.TryParse(portRaw, out var port))
        {
            throw new InvalidOperationException("SMTP_PORT no es un puerto válido.");
        }

        using var client = new SmtpClient(host, port)
        {
            Credentials = new NetworkCredential(user, pass),
            EnableSsl = true
        };

        using var message = new MailMessage(user, correo.Destinatario, correo.Asunto, correo.Cuerpo)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(message, stoppingToken);
    }
}
