using AccessControl.Domain.Entities;
using AccessControl.Domain.Interfaces;
using AccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Repositories;

public class LoginExternoRepository : ILoginExternoRepository
{
    private readonly ApplicationDbContext _dbContext;

    public LoginExternoRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LoginExterno?> ObtenerPorSubjectAsync(string proveedor, string subject)
    {
        return await _dbContext.LoginsExternos
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Proveedor == proveedor && l.Subject == subject);
    }

    public async Task<LoginExterno?> ObtenerDeUsuarioAsync(Guid usuarioId, string proveedor)
    {
        return await _dbContext.LoginsExternos
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UsuarioId == usuarioId && l.Proveedor == proveedor);
    }

    public async Task<IReadOnlyList<LoginExterno>> ListarDeUsuarioAsync(Guid usuarioId)
    {
        return await _dbContext.LoginsExternos
            .AsNoTracking()
            .Where(l => l.UsuarioId == usuarioId)
            .OrderBy(l => l.VinculadoEn)
            .ToListAsync();
    }

    public async Task AgregarAsync(LoginExterno vinculo)
    {
        _dbContext.LoginsExternos.Add(vinculo);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<bool> EliminarAsync(Guid usuarioId, string proveedor)
    {
        var filas = await _dbContext.LoginsExternos
            .Where(l => l.UsuarioId == usuarioId && l.Proveedor == proveedor)
            .ExecuteDeleteAsync();

        return filas > 0;
    }
}
