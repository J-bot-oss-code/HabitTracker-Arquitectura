using AccessControl.Application.Interfaces;
using AccessControl.Domain.Entities;
using AccessControl.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Services;

public class DatabaseEmailQueue : IEmailQueue
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<DatabaseEmailQueue> _logger;

    public DatabaseEmailQueue(ApplicationDbContext dbContext, ILogger<DatabaseEmailQueue> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task EncolarCorreoRecuperacionAsync(string correoDestino, string codigoGenerado)
    {
        _logger.LogInformation("Encolando correo de recuperación para: {Destino}", correoDestino);

        var correo = new CorreoEnCola(
            correoDestino,
            "Recuperación de contraseña — HabitTracker Nexus",
            PlantillaHtml(
                "Recuperación de contraseña",
                $"Hola,<br><br>Recibimos una solicitud para restablecer la contraseña de tu cuenta en <strong>HabitTracker Nexus</strong>. Usa el siguiente código (un solo uso y con fecha de vencimiento):",
                codigoGenerado,
                "Si no solicitaste este cambio, puedes ignorar este mensaje."));

        _dbContext.CorreosEnCola.Add(correo);
        await _dbContext.SaveChangesAsync();
    }

    public async Task EncolarCorreoActivacionAsync(string correo, string token)
    {
        _logger.LogInformation("Encolando correo de activación para: {Destino}", correo);

        var mensaje = new CorreoEnCola(
            correo,
            "Activa tu cuenta — HabitTracker Nexus",
            PlantillaHtml(
                "Bienvenido a HabitTracker Nexus",
                $"Hola,<br><br>Gracias por registrarte en <strong>HabitTracker Nexus</strong>. Para completar tu registro, usa el siguiente token de activación (vence en 24 horas):",
                token,
                "Si no creaste esta cuenta, puedes ignorar este mensaje."));

        _dbContext.CorreosEnCola.Add(mensaje);
        await _dbContext.SaveChangesAsync();
    }

    private static string PlantillaHtml(string titulo, string introduccion, string codigo, string despedida)
    {
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
<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0""><tr><td align=""center"" style=""padding:8px 0 24px 0;"">
<div style=""display:inline-block;background-color:#f0fdfa;border:2px dashed #0f766e;border-radius:8px;padding:16px 32px;font-size:22px;font-weight:bold;letter-spacing:3px;color:#0f766e;"">{codigo}</div>
</td></tr></table>
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
