using AccessControl.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Services;

/// <summary>
/// Adapter temporal de auditoría: registra en el log estructurado.
/// La persistencia real en RegistroAuditoria llega con la Pieza 6 (Auditoría).
/// </summary>
public class AuditoriaService : IAuditoriaService
{
    private readonly ILogger<AuditoriaService> _logger;

    public AuditoriaService(ILogger<AuditoriaService> logger)
    {
        _logger = logger;
    }

    public Task RegistroAuditoriaAsync(
        Guid usuarioId,
        string nombreUsuarioResponsable,
        string accion,
        string entidadAfectada,
        string identificador,
        string valorAnterior,
        string valorNuevo)
    {
        _logger.LogInformation(
            "Auditoría | Usuario: {UsuarioId} ({Responsable}) | Acción: {Accion} | Entidad: {Entidad} | Id: {Identificador} | Antes: {Anterior} | Después: {Nuevo}",
            usuarioId, nombreUsuarioResponsable, accion, entidadAfectada, identificador, valorAnterior, valorNuevo);

        return Task.CompletedTask;
    }
}
