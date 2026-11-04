using DocumentManager.Application.DTOs;

namespace DocumentManager.Application.Interfaces;

public interface IDocumentoService
{
    Task<DocumentoDto> SubirAsync(SubirDocumentoCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentoDto>> ListarAsync(Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default);

    Task<DocumentoDto> ObtenerAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default);

    Task<ArchivoDescargaDto> DescargarAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default);

    Task EliminarAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default);
}
