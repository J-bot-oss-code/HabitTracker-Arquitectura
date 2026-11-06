using AccessControl.Domain.Entities;
using AccessControl.Domain.Interfaces;
using AccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Repositories;

public class ConsentimientoRepository : IConsentimientoRepository
{
    private readonly ApplicationDbContext _dbContext;

    public ConsentimientoRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ConsentimientoUsuario?> ObtenerVigenteAsync(Guid usuarioId)
    {
        // El historial es de solo inserción: el vigente es la última fila escrita.
        return await _dbContext.ConsentimientosUsuario
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.ActualizadoEn)
            .ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<ConsentimientoUsuario>> ListarHistorialAsync(Guid usuarioId)
    {
        return await _dbContext.ConsentimientosUsuario
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .OrderBy(c => c.ActualizadoEn)
            .ThenBy(c => c.Id)
            .ToListAsync();
    }

    public async Task AgregarAsync(ConsentimientoUsuario consentimiento)
    {
        _dbContext.ConsentimientosUsuario.Add(consentimiento);
        await _dbContext.SaveChangesAsync();
    }
}
