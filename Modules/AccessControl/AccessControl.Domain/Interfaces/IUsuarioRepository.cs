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
    /// <summary>
    /// Reserva de forma ATÓMICA un intento de activación y devuelve el total de intentos tras reservarlo
    /// (0 si la cuenta no tiene un código pendiente). Debe ser una sola sentencia en la base de datos: con
    /// leer-incrementar-guardar, peticiones en paralelo leerían el mismo contador y el límite no serviría.
    /// </summary>
    Task<int> ReservarIntentoActivacionAsync(Guid usuarioId);
    Task<IEnumerable<Usuario>> ObtenerTodosAsync();

    /// <summary>Cuántos administradores activos hay. Sirve para no dejar nunca al sistema sin ninguno (RF-CA-08, RF-CA-20).</summary>
    Task<int> ContarAdministradoresActivosAsync();

}
