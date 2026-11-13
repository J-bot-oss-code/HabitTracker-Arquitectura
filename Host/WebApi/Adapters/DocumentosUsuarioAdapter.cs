using DocumentManager.Application.Exceptions;
using DocumentManager.Application.Interfaces;
using HabitTracker.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Puerto <see cref="IDocumentosUsuario"/> de HabitTracker hacia el Manejador de documentos. Código de composición del Host: el
/// Manejador no conoce a HabitTracker (RD-03). Un archivo ajeno, eliminado o inexistente se ve igual: <c>null</c>.
/// </summary>
public sealed class DocumentosUsuarioAdapter(IDocumentoService documentos) : IDocumentosUsuario
{
    public async Task<DocumentoResumen?> ObtenerAsync(Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        try
        {
            var d = await documentos.ObtenerAsync(documentoId, usuarioId, esAdministrador: false, cancellationToken);
            return new DocumentoResumen(d.Id, d.Nombre, d.Tipo, d.TamanoBytes, d.FechaSubidaUtc);
        }
        catch (Exception ex) when (ex is DocumentoNoEncontradoException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
