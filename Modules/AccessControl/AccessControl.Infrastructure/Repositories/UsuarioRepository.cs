
using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Interfaces;
using AccessControl.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly ApplicationDbContext _dbContext;
    public UsuarioRepository(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<bool> CorreoExistsAsync(Email correo)
    {
        // Implementación para verificar si el correo existe en la base de datos
        return await _dbContext.usuarios.AnyAsync(u => u.Correo.Valor == correo.Valor);
        
    }

    public async Task<Usuario?> GetByCorreoAsync(Email correo)
    {
       // Implementación para obtener un usuario por correo electrónico
        return await _dbContext.usuarios.FirstOrDefaultAsync(u => u.Correo.Valor == correo.Valor);
    }

    public async Task<Usuario?> GetByIdAsync(Guid id)
    {
        // Implementación para obtener un usuario por ID
        return await _dbContext.usuarios.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<Usuario?> GetByNameAsync(string nombreCompleto)
    {
        // Implementación para obtener un usuario por nombre completo
        return await _dbContext.usuarios.FirstOrDefaultAsync(u => u.NombreCompleto == nombreCompleto);
    }

    public async Task<Usuario?> GetByTokenRecuperacionAsync(string tokenRecuperacion)
    {
        // implementacion para obtener un usuario por token de recuperacion
        return await _dbContext.usuarios.FirstOrDefaultAsync(u => u.TokenAcceso != null && u.TokenAcceso.Valor == tokenRecuperacion);
    }

    public async Task SaveAsync(Usuario usuario)
    {
        // Implementación para guardar un nuevo usuario en la base de datos
        _dbContext.usuarios.Add(usuario);
        await _dbContext.SaveChangesAsync();
    }

    public async Task UpdateAsync(Usuario usuario)
    {
        _dbContext.usuarios.Update(usuario);
        await _dbContext.SaveChangesAsync();
    }
}
