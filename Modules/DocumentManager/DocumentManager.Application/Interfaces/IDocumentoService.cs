using DocumentManager.Application.DTOs;

namespace DocumentManager.Application.Interfaces;

public interface IDocumentoService
{
    Task<DocumentoDto> SubirAsync(SubirDocumentoCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentoDto>> ListarAsync(Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default);

    Task<DocumentoDto> ObtenerAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default);

    Task<ArchivoDescargaDto> DescargarAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default);

    Task EliminarAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default);

    /// <summary>
    /// Baja de cuenta: borra definitivamente los archivos del disco y los registros de TODOS los documentos del usuario,
    /// también los que ya estaban marcados como eliminados. No cambia el borrado lógico de RF-DOC-04 para el uso normal.
    /// Idempotente. Devuelve cuántos documentos borró.
    /// </summary>
    Task<int> PurgarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
