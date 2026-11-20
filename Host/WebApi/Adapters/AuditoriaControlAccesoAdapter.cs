using AccessControl.Application.Interfaces;
using AccessControl.Domain.Interfaces;
using Auditing.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="IAuditoriaService"/> de Control de Acceso hacia el historial de Auditoría. Código de composición del Host:
/// ninguno de los dos módulos conoce al otro (RD-03). Control de Acceso sigue registrando el evento donde ocurre el cambio y aquí
/// se guarda de verdad (antes solo iba al log). Un fallo al guardar NO se traga: la operación responde error. El cambio ya
/// quedó guardado (no comparten transacción: límite declarado en el README), así que el error avisa de que falta su registro.
/// </summary>
public sealed class AuditoriaControlAccesoAdapter(IRegistroAuditoriaService historial, IUsuarioRepository usuarios) : IAuditoriaService
{
    /// <remarks>
    /// El segundo parámetro llega con el rol de quien actuó (no con su nombre); el historial guarda su correo, que se busca por el
    /// identificador. Si la cuenta ya no existe se usa el identificador, para no perder el registro.
    /// </remarks>
    public async Task RegistroAuditoriaAsync(
        Guid usuarioId, string nombreUsuarioResponsable, string accion, string entidadAfectada, string identificador,
        string valorAnterior, string valorNuevo)
    {
        var actor = await usuarios.GetByIdAsync(usuarioId);
        await historial.RegistrarAsync(
            usuarioId, actor?.Correo.Valor ?? usuarioId.ToString(), accion, entidadAfectada, identificador, valorAnterior, valorNuevo);
    }
}
