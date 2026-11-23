using Microsoft.EntityFrameworkCore;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Services;

public class DatabaseEmailQueue : IEmailQueue
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseEmailQueue> _logger;

    public DatabaseEmailQueue(ApplicationDbContext dbContext, IConfiguration configuration, ILogger<DatabaseEmailQueue> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task EncolarCorreoRecuperacionAsync(string correoDestino, string codigoGenerado)
    {
        var correo = new CorreoEnCola(
            correoDestino,
            "Recuperación de contraseña — HabitTracker Nexus",
            PlantillaHtml(
                "Recuperación de contraseña",
                $"Hola,<br><br>Recibimos una solicitud para restablecer la contraseña de tu cuenta en <strong>HabitTracker Nexus</strong>. Usa el siguiente código (un solo uso y con fecha de vencimiento):",
                codigoGenerado,
                "Si no solicitaste este cambio, puedes ignorar este mensaje."));

        if (!await GuardarAsync(correo, critico: true)) return;

        // El destinatario es un dato personal: se registra solo el identificador del correo en cola.
        _logger.LogInformation("Correo de recuperación en cola: {CorreoId}.", correo.Id);
    }

    public async Task EncolarCorreoActivacionAsync(string correo, string codigo)
    {
        var baseUrl = (_configuration["App:BaseUrl"] ?? "http://localhost:5246").TrimEnd('/');
        var enlace = $"{baseUrl}/api/auth/activate?correo={Uri.EscapeDataString(correo)}&codigo={Uri.EscapeDataString(codigo)}";

        var mensaje = new CorreoEnCola(
            correo,
            "Activa tu cuenta — HabitTracker Nexus",
            PlantillaHtml(
                "Bienvenido a HabitTracker Nexus",
                $"Hola,<br><br>Gracias por registrarte en <strong>HabitTracker Nexus</strong>. Para completar tu registro, pulsa el botón o ingresa este código de 6 dígitos junto con tu correo (vence en 24 horas):",
                codigo,
                "Si no creaste esta cuenta, puedes ignorar este mensaje.",
                [new AccionCorreo("Activar mi cuenta", enlace)]));

        if (!await GuardarAsync(mensaje, critico: true)) return;

        _logger.LogInformation("Correo de activación en cola: {CorreoId}.", mensaje.Id);
    }

    public async Task EncolarCorreoAsync(string destinatario, string asunto, string titulo, string mensaje)
    {
        var correo = new CorreoEnCola(
            destinatario,
            $"{asunto} — HabitTracker Nexus",
            PlantillaHtml(
                Escapar(titulo),
                Escapar(mensaje),
                codigo: null,
                "Puedes dejar de recibir estos correos desde Configuración, en la app."));

        if (!await GuardarAsync(correo, critico: false)) return;

        _logger.LogInformation("Correo informativo en cola: {CorreoId}.", correo.Id);
    }

    public async Task EncolarCorreoConAccionesAsync(
        string destinatario, string asunto, string titulo, string mensaje,
        IReadOnlyList<AccionCorreo> acciones, string? enlaceBaja)
    {
        ArgumentNullException.ThrowIfNull(acciones);
        foreach (var accion in acciones)
        {
            ExigirUrlHttp(accion.Url);
        }

        var despedida = enlaceBaja is null
            ? "Puedes dejar de recibir estos correos desde Configuración, en la app."
            : $@"¿No quieres más recordatorios? <a href=""{ExigirUrlHttp(enlaceBaja)}"" style=""color:#6b7280;"">Darme de baja</a>.";

        var correo = new CorreoEnCola(
            destinatario,
            $"{asunto} — HabitTracker Nexus",
            // Cada salto de línea del mensaje (una lista de tareas) se ve como tal, después de escapar el texto.
            PlantillaHtml(Escapar(titulo), Escapar(mensaje).Replace("\n", "<br>"), codigo: null, despedida, acciones));

        if (!await GuardarAsync(correo, critico: false)) return;

        _logger.LogInformation("Correo con acciones en cola: {CorreoId}.", correo.Id);
    }

    /// <summary>La URL va a un atributo href: solo http(s) absoluta (nada de javascript: ni data:), y escapada.</summary>
    private static string ExigirUrlHttp(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new ArgumentException("La URL del correo debe ser http(s) absoluta.", nameof(url));
        }

        return Escapar(uri.AbsoluteUri);
    }

    /// <summary>Correos a una misma dirección por hora: más que eso es abuso (bombardear a alguien con registros o recuperaciones).</summary>
    public const int TopePorDestinatarioPorHora = 10;

    /// <summary>Parte del tope diario que solo pueden usar la activación y la recuperación (sin ellas nadie puede entrar).</summary>
    public const int ReservaCriticos = 50;

    /// <summary>
    /// Topes contra el agotamiento de la cuota del proveedor (SEG-05): por destinatario y diario global (<c>Correo:TopeDiario</c>, 450
    /// por defecto, lo que permite Gmail en el piloto). Lo que se pasa no se encola y queda en el log, sin el correo de la persona;
    /// la operación que lo pidió sigue (falla el correo, no el registro).
    /// </summary>
    private async Task<bool> GuardarAsync(CorreoEnCola correo, bool critico)
    {
        // shortcut: contar y luego insertar deja un hueco con envíos simultáneos (uno o dos de más); es un tope, no un dato exacto.
        var ahora = DateTime.UtcNow;
        var tope = _configuration.GetValue("Correo:TopeDiario", 450);
        var delDia = await _dbContext.CorreosEnCola.CountAsync(c => c.FechaCreacion >= ahora.AddDays(-1));
        var alDestinatario = await _dbContext.CorreosEnCola.CountAsync(c => c.Destinatario == correo.Destinatario && c.FechaCreacion >= ahora.AddHours(-1));

        if (alDestinatario >= TopePorDestinatarioPorHora || delDia >= (critico ? tope : tope - ReservaCriticos))
        {
            _logger.LogWarning("Correo no encolado: se alcanzó el tope de envíos ({Tipo}).", critico ? "crítico" : "informativo");
            return false;
        }

        _dbContext.CorreosEnCola.Add(correo);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    // Solo los caracteres que cambian el HTML: WebUtility.HtmlEncode también convertiría las tildes en entidades numéricas.
    private static string Escapar(string texto) =>
        texto.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private static string PlantillaHtml(
        string titulo, string introduccion, string? codigo, string despedida, IReadOnlyList<AccionCorreo>? botones = null)
    {
        var caja = string.IsNullOrWhiteSpace(codigo)
            ? string.Empty
            : $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:8px 0 24px 0;"">
<div style=""display:inline-block;background-color:#f0fdfa;border:2px dashed #0f766e;border-radius:8px;padding:16px 32px;font-size:22px;font-weight:bold;letter-spacing:3px;color:#0f766e;"">{codigo}</div>
</td></tr></table>";

        // Un botón por acción, uno debajo de otro (en móvil caben a lo ancho). El texto se escapa; la URL ya viene validada.
        var boton = string.Concat((botones ?? []).Select(b =>
            $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:0 0 12px 0;""><a href=""{ExigirUrlHttp(b.Url)}"" style=""display:inline-block;background-color:#0f766e;color:#ffffff;text-decoration:none;font-weight:bold;padding:14px 36px;border-radius:8px;"">{Escapar(b.Texto)}</a></td></tr></table>"));

        return $@"<!DOCTYPE html>
<html lang=""es"">
<head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width, initial-scale=1.0""></head>
<body style=""margin:0;padding:0;background-color:#f4f6f8;font-family:Arial,Helvetica,sans-serif;"">
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background-color:#f4f6f8;padding:24px 0;"">
<tr><td align=""center"">
<table role=""presentation"" width=""600"" cellpadding=""0"" cellspacing=""0"" style=""max-width:600px;width:100%;background-color:#ffffff;border-radius:8px;overflow:hidden;"">
<tr><td style=""background-color:#0f766e;padding:20px 32px;color:#ffffff;font-size:20px;font-weight:bold;"">HabitTracker Nexus</td></tr>
<tr><td style=""padding:32px;color:#1f2937;font-size:15px;line-height:1.6;"">
<h2 style=""margin:0 0 16px 0;font-size:19px;color:#0f766e;"">{titulo}</h2>
<p style=""margin:0 0 24px 0;"">{introduccion}</p>
{boton}
{caja}
<p style=""margin:0 0 8px 0;font-size:13px;color:#6b7280;"">{despedida}</p>
</td></tr>
<tr><td style=""background-color:#f4f6f8;padding:16px 32px;font-size:12px;color:#9ca3af;text-align:center;"">Este es un mensaje automático, por favor no respondas a este correo.</td></tr>
</table>
</td></tr>
</table>
</body>
</html>";
    }
}
