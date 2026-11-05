using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using AccessControl.Domain.Excepciones;
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

    public Task SaveAsync(Usuario usuario)
    {
        _dbContext.usuarios.Add(usuario);
        return GuardarNuevoAsync(usuario);
    }

    public Task SaveAsync(Usuario usuario, ConsentimientoUsuario consentimiento)
    {
        // Un solo SaveChanges = una sola transacción: o se guardan el usuario y su consentimiento, o ninguno.
        _dbContext.usuarios.Add(usuario);
        _dbContext.ConsentimientosUsuario.Add(consentimiento);
        return GuardarNuevoAsync(usuario);
    }

    private async Task GuardarNuevoAsync(Usuario usuario)
    {
        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Dos registros simultáneos con el mismo correo: el segundo choca con el índice único (RF-CA-01).
            // Se traduce al mismo rechazo que la comprobación previa; no importa el proveedor de base de datos.
            // Cualquier otro fallo de la base de datos se propaga tal cual.
            if (await ElCorreoYaExisteTrasElFalloAsync(usuario))
            {
                throw new CorreoYaRegistradoException();
            }

            throw;
        }
    }

    public async Task UpdateAsync(Usuario usuario)
    {
        // Una entidad ya cargada por este contexto solo escribe las columnas que cambiaron, sin marcar todas como
        // modificadas: así este guardado nunca toca el contador de intentos de activación que otra petición reserva con
        // ReservarIntentoActivacionAsync (RD-06). Solo una entidad creada fuera del contexto necesita Update().
        if (_dbContext.Entry(usuario).State == EntityState.Detached)
        {
            _dbContext.usuarios.Update(usuario);
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task<bool> ElCorreoYaExisteTrasElFalloAsync(Usuario usuario)
    {
        // El intento fallido deja las entidades nuevas en el contexto: se sacan para poder consultar y para no reintentar su inserción.
        _dbContext.Entry(usuario).State = EntityState.Detached;
        foreach (var consentimiento in _dbContext.ChangeTracker.Entries<ConsentimientoUsuario>()
                     .Where(e => e.State == EntityState.Added).ToList())
        {
            consentimiento.State = EntityState.Detached;
        }

        return await CorreoExistsAsync(usuario.Correo);
    }

    public async Task<IEnumerable<Usuario>> ObtenerTodosAsync()
    {
        return await _dbContext.usuarios.ToListAsync();
    }

    public async Task<int> ContarAdministradoresActivosAsync()
    {
        return await _dbContext.usuarios.CountAsync(u => u.Rol == Rol.Administrador && u.Activo);
    }
}
