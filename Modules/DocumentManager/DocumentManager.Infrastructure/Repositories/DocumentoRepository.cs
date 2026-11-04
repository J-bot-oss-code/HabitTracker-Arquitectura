using DocumentManager.Application.Interfaces;
using DocumentManager.Domain.Entities;
using DocumentManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DocumentManager.Infrastructure.Repositories;

public sealed class DocumentoRepository(DocumentManagerDbContext contexto) : IDocumentoRepository
{
    public async Task AddAsync(Documento documento, CancellationToken cancellationToken = default)
    {
        contexto.Documentos.Add(documento);
        await contexto.SaveChangesAsync(cancellationToken);
    }

    // RF-DOC-04: no existe un método Remove. Eliminar es un UPDATE de la marca Eliminado.
    public async Task UpdateAsync(Documento documento, CancellationToken cancellationToken = default)
    {
        contexto.Documentos.Update(documento);
        await contexto.SaveChangesAsync(cancellationToken);
    }

    public Task<Documento?> ObtenerPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        contexto.Documentos.FirstOrDefaultAsync(documento => documento.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Documento>> ListarNoEliminadosAsync(Guid? usuarioId, CancellationToken cancellationToken = default)
    {
        var consulta = contexto.Documentos.AsNoTracking().Where(documento => !documento.Eliminado);

        if (usuarioId is not null)
        {
            consulta = consulta.Where(documento => documento.UsuarioId == usuarioId);
        }

        return await consulta
            .OrderByDescending(documento => documento.FechaSubidaUtc)
            .ToListAsync(cancellationToken);
    }
}
