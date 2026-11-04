using DocumentManager.Domain.Entities;

namespace DocumentManager.Application.Interfaces;

/// <summary>
/// Puerto de persistencia de los metadatos. La aplicación no sabe si detrás hay
/// SQL Server, SQLite o una lista en memoria (RD-01).
/// </summary>
public interface IDocumentoRepository
{
    Task AddAsync(Documento documento, CancellationToken cancellationToken = default);

    Task UpdateAsync(Documento documento, CancellationToken cancellationToken = default);

    /// <summary>Devuelve el registro aunque esté marcado como eliminado; el servicio decide qué hacer.</summary>
    Task<Documento?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Devuelve solo documentos NO eliminados. Con <paramref name="usuarioId"/> filtra por dueño;
    /// con null devuelve los de todos (uso exclusivo del Administrador).
    /// </summary>
    Task<IReadOnlyList<Documento>> ListarNoEliminadosAsync(Guid? usuarioId, CancellationToken cancellationToken = default);
}
