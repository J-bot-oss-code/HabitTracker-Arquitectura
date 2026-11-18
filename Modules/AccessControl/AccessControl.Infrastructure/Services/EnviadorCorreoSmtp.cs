using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Services;

/// <summary>Entrega un correo al servidor de correo (o a la consola en desarrollo). Falla con una excepción si no se pudo entregar.</summary>
public interface IEnviadorCorreo
{
    Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, CancellationToken cancellationToken);
}

public class EnviadorCorreoSmtp(IConfiguration configuration, ILogger<EnviadorCorreoSmtp> logger) : IEnviadorCorreo
{
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

    public async Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, CancellationToken cancellationToken)
    {
        if (DebeEscribirEnConsola(configuration))
        {
            // Canal de desarrollo para poder activar cuentas y recuperar contraseñas sin un servidor de correo.
            // El cuerpo incluye el enlace o el código: en producción se debe usar SMTP real.
            // Una sola línea: el cuerpo HTML ocupa decenas de líneas y filtrar el log por "CORREO" no mostraría el enlace.
            logger.LogInformation(
                "[CORREO EN CONSOLA] Para: {Destino} | Asunto: {Asunto} | {Resumen}",
                destinatario, asunto, ResumirCuerpo(cuerpoHtml));
            return;
        }

        // RF-NOT-13 / RD-10: credenciales obligatoriamente desde configuración (variables de entorno).
        var host = configuration["SMTP_HOST"];
        var portRaw = configuration["SMTP_PORT"];
        var user = configuration["SMTP_USER"];
        var pass = configuration["SMTP_PASS"];

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

        using var message = new MailMessage(user, destinatario, asunto, cuerpoHtml)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
