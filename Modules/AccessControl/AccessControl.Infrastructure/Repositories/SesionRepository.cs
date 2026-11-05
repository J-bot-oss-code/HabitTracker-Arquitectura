using AccessControl.Domain.Entities;
using AccessControl.Domain.Interfaces;
using AccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Repositories;

public class SesionRepository : ISesionRepository
{
    private readonly ApplicationDbContext _dbContext;

    public SesionRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AgregarAsync(SesionUsuario sesion)
    {
        _dbContext.SesionesUsuario.Add(sesion);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<SesionUsuario?> ObtenerPorHashAsync(string tokenHash)
    {
        return await _dbContext.SesionesUsuario
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash);
    }

    public async Task<SesionUsuario?> ObtenerAsync(Guid usuarioId, Guid sesionId)
    {
        return await _dbContext.SesionesUsuario
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sesionId && s.UsuarioId == usuarioId);
    }

    public async Task<IReadOnlyList<SesionUsuario>> ListarActivasAsync(Guid usuarioId, DateTime ahora)
    {
        return await _dbContext.SesionesUsuario
            .AsNoTracking()
            .Where(s => s.UsuarioId == usuarioId && s.RevocadaEn == null && s.ExpiraEn > ahora)
            .OrderByDescending(s => s.UltimoUsoEn)
            .ThenBy(s => s.Id)
            .ToListAsync();
    }

    public async Task<bool> RotarAsync(Guid sesionActualId, SesionUsuario siguiente, DateTime ahora)
    {
        await using var transaccion = await _dbContext.Database.BeginTransactionAsync();

        _dbContext.SesionesUsuario.Add(siguiente);
        await _dbContext.SaveChangesAsync();

        // Una sola sentencia: solo una de las peticiones simultáneas con el mismo token encuentra la sesión sin revocar.
        var filas = await _dbContext.SesionesUsuario
            .Where(s => s.Id == sesionActualId && s.RevocadaEn == null)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.RevocadaEn, ahora)
                .SetProperty(s => s.ReemplazadaPorId, siguiente.Id)
                .SetProperty(s => s.UltimoUsoEn, ahora));

        if (filas == 0)
        {
            await transaccion.RollbackAsync();
            _dbContext.Entry(siguiente).State = EntityState.Detached;
            return false;
        }

        await transaccion.CommitAsync();
        return true;
    }

    public async Task<int> RevocarFamiliaAsync(Guid familiaId, DateTime ahora)
    {
        return await _dbContext.SesionesUsuario
            .Where(s => s.FamiliaId == familiaId && s.RevocadaEn == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevocadaEn, ahora));
    }

    public async Task<int> RevocarTodasAsync(Guid usuarioId, DateTime ahora)
    {
        return await _dbContext.SesionesUsuario
            .Where(s => s.UsuarioId == usuarioId && s.RevocadaEn == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevocadaEn, ahora));
    }

    public async Task<int> EliminarVencidasAsync(DateTime venciaAntesDe)
    {
        return await _dbContext.SesionesUsuario
            .Where(s => s.ExpiraEn < venciaAntesDe)
            .ExecuteDeleteAsync();
    }
}
