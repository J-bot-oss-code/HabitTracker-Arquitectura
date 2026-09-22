using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;

namespace AccessControl.Domain.Interfaces;

public interface IUsuarioRepository
{
    Task<Usuario?> GetByIdAsync(Guid id);
    Task<Usuario?> GetByCorreoAsync(Email correo);
    Task SaveAsync(Usuario usuario);
    Task<bool> CorreoExistsAsync(Email correo);
    Task UpdateAsync(Usuario usuario);
    Task<Usuario?> GetByNameAsync(string nombreCompleto);
    Task<Usuario?> GetByTokenRecuperacionAsync(string token);

}
