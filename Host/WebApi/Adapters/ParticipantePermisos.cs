using AccessControl.Application.Interfaces;
using Permissions.Application.DTOs;
using Permissions.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Solicitudes de permiso en la descarga de datos y en la baja de cuenta. El módulo identifica al solicitante por su correo.
/// </summary>
public sealed class ParticipantePermisos(ISolicitudPermisoService servicio) : IParticipanteDatosUsuario
{
    public string Seccion => "permisos";

    public async Task<object> ExportarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        // Se consulta como persona de rol Estandar: solo sus propias solicitudes, también las ya resueltas.
        await servicio.ConsultarAsync(new ObtenerSolicitudesQuery(correo, "Estandar", SoloPendientes: false), cancelacion);

    public async Task EliminarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.EliminarDeSolicitanteAsync(correo, cancelacion);
}
