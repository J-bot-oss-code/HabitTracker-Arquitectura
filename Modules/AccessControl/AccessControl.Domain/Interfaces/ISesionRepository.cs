using AccessControl.Domain.Entities;

namespace AccessControl.Domain.Interfaces;

public interface ISesionRepository
{
    Task AgregarAsync(SesionUsuario sesion);

    /// <summary>Busca por el hash del refresh token (nunca por el token).</summary>
    Task<SesionUsuario?> ObtenerPorHashAsync(string tokenHash);

    /// <summary>Una sesión del usuario indicado. Si es de otro usuario no se devuelve: nadie toca sesiones ajenas.</summary>
    Task<SesionUsuario?> ObtenerAsync(Guid usuarioId, Guid sesionId);

    Task<IReadOnlyList<SesionUsuario>> ListarActivasAsync(Guid usuarioId, DateTime ahora);

    /// <summary>
    /// Sustituye la sesión actual por la siguiente de su familia de forma ATÓMICA: la actual solo se revoca si seguía
    /// activa (una sola sentencia en la base de datos) y la nueva se guarda en la misma transacción. Devuelve false si
    /// otra petición ya había usado esa sesión: con leer-comprobar-guardar, dos renovaciones simultáneas con el mismo
    /// token tendrían éxito las dos.
    /// </summary>
    Task<bool> RotarAsync(Guid sesionActualId, SesionUsuario siguiente, DateTime ahora);

    Task<int> RevocarFamiliaAsync(Guid familiaId, DateTime ahora);

    Task<int> RevocarTodasAsync(Guid usuarioId, DateTime ahora);

    /// <summary>Borra las sesiones que vencieron antes de la fecha indicada.</summary>
    Task<int> EliminarVencidasAsync(DateTime venciaAntesDe);
}
