using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using Auditing.Application.Interfaces;
using Permissions.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Lleva al historial de auditoría la resolución de una solicitud de permiso (RF-AUD-02). Código de composición del Host: Permisos
/// y Auditoría no se conocen (RD-03). Permisos solo conoce el correo del Administrador (viene del token); el historial pide su
/// identificador, que se busca en Control de Acceso. Un fallo al guardar no se traga.
/// </summary>
public sealed class AuditorPermisosAdapter(IRegistroAuditoriaService historial, IUsuarioRepository usuarios) : IAuditorPermisos
{
    public async Task ResolucionRegistradaAsync(
        string aprobador, Guid solicitudId, string accion, string valorAnterior, string valorNuevo, CancellationToken cancellationToken = default)
    {
        var admin = await usuarios.GetByCorreoAsync(new Email(aprobador))
            ?? throw new InvalidOperationException("No se pudo identificar a quien resolvió la solicitud.");

        await historial.RegistrarAsync(
            admin.Id, admin.Correo.Valor, accion, "SolicitudPermiso", solicitudId.ToString(), valorAnterior, valorNuevo, cancellationToken);
    }
}
