namespace AccessControl.Application.Interfaces;

/// <summary>
/// Un módulo que guarda datos de un usuario y participa en su descarga y en su borrado. Control de Acceso no conoce a los
/// módulos de negocio (RD-03): cada uno se registra desde el Host con un adaptador que implementa esta interfaz y llama a
/// su propio servicio de Application. Ningún módulo lee las tablas de otro.
/// </summary>
public interface IParticipanteDatosUsuario
{
    /// <summary>Nombre de la sección en la descarga de datos (por ejemplo "habitos").</summary>
    string Seccion { get; }

    /// <summary>Los datos que el módulo guarda de esa persona, listos para serializar a JSON.</summary>
    Task<object> ExportarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default);

    /// <summary>
    /// Borra definitivamente los datos de esa persona. Debe ser idempotente: si falla a medias, la baja se reintenta en la
    /// siguiente pasada y la segunda vez no encontrará lo que ya se borró.
    /// </summary>
    Task EliminarAsync(Guid usuarioId, string correo, CancellationToken cancelacion = default);
}
