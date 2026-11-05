
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

    public async Task<int> ReservarIntentoActivacionAsync(Guid usuarioId)
    {
        // Una sola sentencia: el motor serializa los UPDATE sobre la misma fila, así que N peticiones en paralelo
        // obtienen N valores distintos (1, 2, 3...) y solo las primeras 5 llegan a comparar el código.
        var conexion = _dbContext.Database.GetDbConnection();
        var estabaAbierta = conexion.State == System.Data.ConnectionState.Open;
        if (!estabaAbierta)
        {
            await conexion.OpenAsync();
        }

        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText =
                "UPDATE usuarios SET IntentosActivacion = ISNULL(IntentosActivacion, 0) + 1 " +
                "OUTPUT inserted.IntentosActivacion " +
                "WHERE Id = @id AND Activo = 0 AND TokenAcceso IS NOT NULL";

            var parametro = comando.CreateParameter();
            parametro.ParameterName = "@id";
            parametro.Value = usuarioId;
            comando.Parameters.Add(parametro);

            var resultado = await comando.ExecuteScalarAsync();
            return resultado is null or DBNull ? 0 : Convert.ToInt32(resultado);
        }
        finally
        {
            if (!estabaAbierta)
            {
                await conexion.CloseAsync();
            }
        }
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

    public async Task<IEnumerable<Usuario>> ObtenerTodosAsync()
    {
        return await _dbContext.usuarios.ToListAsync();
    }
}
