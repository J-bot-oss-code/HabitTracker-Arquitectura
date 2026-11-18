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

    public sealed record OpcionesSmtp(string Host, int Puerto, bool Ssl, string? Usuario, string? Clave, string Remitente);

    /// <summary>
    /// RF-NOT-13 / RD-10: el servidor de correo se configura solo con variables de entorno: <c>SMTP_HOST</c> y <c>SMTP_PORT</c>
    /// (obligatorias), <c>SMTP_SSL</c> (true por defecto), <c>SMTP_USER</c> y <c>SMTP_PASS</c> (ambas o ninguna: un servidor de
    /// pruebas como Mailpit no pide credenciales) y <c>SMTP_FROM</c> (por defecto, el usuario).
    /// </summary>
    public static OpcionesSmtp LeerOpciones(IConfiguration configuration)
    {
        var host = configuration["SMTP_HOST"]?.Trim();
        var puertoTexto = configuration["SMTP_PORT"]?.Trim();
        var usuario = configuration["SMTP_USER"]?.Trim();
        var clave = configuration["SMTP_PASS"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(puertoTexto))
        {
            throw new InvalidOperationException("Falta configuración SMTP (SMTP_HOST y SMTP_PORT).");
        }

        if (!int.TryParse(puertoTexto, out var puerto) || puerto is < 1 or > 65535)
        {
            throw new InvalidOperationException("SMTP_PORT no es un puerto válido.");
        }

        var sslTexto = configuration["SMTP_SSL"]?.Trim();
        var ssl = string.IsNullOrEmpty(sslTexto) || (bool.TryParse(sslTexto, out var valor)
            ? valor
            : throw new InvalidOperationException("SMTP_SSL debe ser true o false."));

        if (string.IsNullOrWhiteSpace(usuario) != string.IsNullOrEmpty(clave))
        {
            throw new InvalidOperationException("SMTP_USER y SMTP_PASS van juntas: define las dos o ninguna.");
        }

        var autenticado = !string.IsNullOrWhiteSpace(usuario);
        var remitente = configuration["SMTP_FROM"]?.Trim();
        if (string.IsNullOrWhiteSpace(remitente))
        {
            remitente = autenticado ? usuario! : "no-responder@habittracker.local";
        }

        return new OpcionesSmtp(host, puerto, ssl, autenticado ? usuario : null, autenticado ? clave : null, remitente);
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

        var opciones = LeerOpciones(configuration);

        using var client = new SmtpClient(opciones.Host, opciones.Puerto)
        {
            EnableSsl = opciones.Ssl,
            Timeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds
        };
        if (opciones.Usuario is not null)
        {
            client.Credentials = new NetworkCredential(opciones.Usuario, opciones.Clave);
        }

        using var message = new MailMessage(opciones.Remitente, destinatario, asunto, cuerpoHtml)
        {
            IsBodyHtml = true
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
