using AccessControl.Application.DTOs;

namespace AccessControl.Application.Interfaces;

/// <summary>Consulta y operación de la cola de correos para el Administrador (RF-NOT-09 y RF-NOT-11).</summary>
public interface IColaCorreosAdmin
{
    /// <summary>
    /// Los correos más recientes (hasta 200), opcionalmente de un solo estado (<c>Pendiente</c>, <c>Procesando</c>,
    /// <c>Enviado</c> o <c>Fallido</c>). Un estado desconocido lanza <see cref="ArgumentException"/> (400).
    /// </summary>
    Task<IReadOnlyList<CorreoColaDto>> ListarAsync(string? estado, CancellationToken cancellationToken = default);

    /// <summary>Ejecuta una pasada del procesador de la cola, igual que el servicio en segundo plano.</summary>
    Task<ResultadoColaDto> ProcesarAsync(CancellationToken cancellationToken = default);
}
