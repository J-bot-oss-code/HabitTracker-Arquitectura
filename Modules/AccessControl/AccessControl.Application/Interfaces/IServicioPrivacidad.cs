using AccessControl.Application.DTOs;

namespace AccessControl.Application.Interfaces;

/// <summary>Derechos de la persona sobre su cuenta: darse de baja, cancelar la baja y descargar sus datos.</summary>
public interface IServicioPrivacidad
{
    public const string ModoDesactivar = "desactivar";
    public const string ModoEliminar = "eliminar";

    /// <summary>
    /// Desactiva la cuenta o programa su eliminación, confirmando con la contraseña (o con Google si la cuenta no tiene).
    /// Una confirmación incorrecta es siempre un 400 y cuenta para el bloqueo por intentos fallidos (RF-CA-19); nunca un 401,
    /// que haría que el cliente cierre la sesión de quien solo se equivocó. Revoca todas las sesiones.
    /// </summary>
    Task<BajaSolicitadaDto> SolicitarBajaAsync(Guid usuarioId, string modo, string? password, string? idTokenGoogle);

    /// <summary>Cancela la baja programada. False si no había ninguna.</summary>
    Task<bool> CancelarBajaAsync(Guid usuarioId);

    /// <summary>Fecha en que se borrará la cuenta, o null si no hay una baja programada.</summary>
    Task<DateTime?> ObtenerBajaProgramadaAsync(Guid usuarioId);

    Task<ExportacionDatosDto> ExportarAsync(Guid usuarioId, CancellationToken cancelacion = default);
}
