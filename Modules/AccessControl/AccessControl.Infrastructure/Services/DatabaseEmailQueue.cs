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
            "Recuperación de contraseña",
            $"Tu código de recuperación es: {codigoGenerado}. Tiene un solo uso y fecha de vencimiento.");

        _dbContext.CorreosEnCola.Add(correo);
        await _dbContext.SaveChangesAsync();
    }

    public async Task EncolarCorreoActivacionAsync(string correo, string token)
    {
        _logger.LogInformation("Encolando correo de activación para: {Destino}", correo);

        var mensaje = new CorreoEnCola(
            correo,
            "Activación de cuenta",
            $"Activa tu cuenta con este token: {token}. Vence en 24 horas.");

        _dbContext.CorreosEnCola.Add(mensaje);
        await _dbContext.SaveChangesAsync();
    }
}
