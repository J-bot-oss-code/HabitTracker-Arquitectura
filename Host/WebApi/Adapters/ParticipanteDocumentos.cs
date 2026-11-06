using AccessControl.Application.Interfaces;
using DocumentManager.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Documentos en la descarga de datos (sus metadatos; el contenido se baja por /api/documents/{id}/download) y en la baja de
/// cuenta (se borran los archivos del disco y los registros, también los que estaban marcados como eliminados).
/// </summary>
public sealed class ParticipanteDocumentos(IDocumentoService servicio) : IParticipanteDatosUsuario
{
    public string Seccion => "documentos";

    public async Task<object> ExportarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.ListarAsync(usuarioId, esAdministrador: false, cancelacion);

    public async Task EliminarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default) =>
        await servicio.PurgarDeUsuarioAsync(usuarioId, cancelacion);
}
