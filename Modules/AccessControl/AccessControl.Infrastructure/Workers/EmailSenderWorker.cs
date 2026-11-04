using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
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

        var ahora = DateTime.UtcNow;

        var candidatos = await dbContext.CorreosEnCola
            .Where(c => c.Estado == EstadoCorreo.Pendiente || c.Estado == EstadoCorreo.Fallido)
            .ToListAsync(stoppingToken);

        var pendientes = candidatos.Where(c => c.PuedeReintentarse(ahora)).ToList();

        if (pendientes.Count == 0)
        {
            return;
        }

        _logger.LogInformation("EmailSenderWorker encontró {Count} correos pendientes.", pendientes.Count);

        foreach (var correo in pendientes)
        {
            if (correo.Estado == EstadoCorreo.Fallido)
            {
                correo.Reintentar();
            }

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
                correo.RegistrarFallo(ex.Message);
                await dbContext.SaveChangesAsync(stoppingToken);

                _logger.LogWarning(ex, "Fallo el envío del correo {Id} a {Destino} (intento {Intentos}).", correo.Id, correo.Destinatario, correo.Intentos);
            }
        }
    }

    /// <summary>
    /// Modo consola: solo si se pide EXPLÍCITAMENTE (CORREO_MODO=consola) y no hay un servidor SMTP configurado.
    /// Si el operador define SMTP_HOST, el envío real tiene prioridad aunque olvide quitar CORREO_MODO.
    /// </summary>
    public static bool DebeEscribirEnConsola(IConfiguration configuration)
    {
        var modo = configuration["CORREO_MODO"];
        var host = configuration["SMTP_HOST"];

        return string.Equals(modo?.Trim(), "consola", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(host);
    }

    /// <summary>
    /// Resume el correo HTML en una línea: el enlace del botón (activación) y el código destacado
    /// (token de activación o código de recuperación). Si no encuentra ninguno, devuelve el texto sin etiquetas.
    /// </summary>
    public static string ResumirCuerpo(string cuerpoHtml)
    {
        if (string.IsNullOrWhiteSpace(cuerpoHtml))
        {
            return string.Empty;
        }

        var partes = new List<string>();

        var enlace = Regex.Match(cuerpoHtml, "href=\"(?<u>[^\"]+)\"");
        if (enlace.Success)
        {
            partes.Add($"Enlace: {System.Net.WebUtility.HtmlDecode(enlace.Groups["u"].Value)}");
        }

        var codigo = Regex.Match(cuerpoHtml, @"<div[^>]*letter-spacing[^>]*>(?<c>[^<]+)</div>");
        if (codigo.Success)
        {
            partes.Add($"Código: {codigo.Groups["c"].Value.Trim()}");
        }

        if (partes.Count > 0)
        {
            return string.Join(" | ", partes);
        }

        var texto = Regex.Replace(Regex.Replace(cuerpoHtml, "<[^>]+>", " "), @"\s+", " ").Trim();
        return texto.Length <= 500 ? texto : texto[..500];
    }

    private async Task EnviarPorSmtpAsync(CorreoEnCola correo, CancellationToken stoppingToken)
    {
        if (DebeEscribirEnConsola(_configuration))
        {
            // Canal de desarrollo para poder activar cuentas y recuperar contraseñas sin un servidor de correo.
            // El cuerpo incluye el enlace o el código: en producción se debe usar SMTP real.
            // Una sola línea: el cuerpo HTML ocupa decenas de líneas y filtrar el log por "CORREO" no mostraría el enlace.
            _logger.LogInformation(
                "[CORREO EN CONSOLA] Para: {Destino} | Asunto: {Asunto} | {Resumen}",
                correo.Destinatario, correo.Asunto, ResumirCuerpo(correo.Cuerpo));
            return;
        }

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
