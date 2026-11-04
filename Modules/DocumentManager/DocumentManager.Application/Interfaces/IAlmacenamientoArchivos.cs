namespace DocumentManager.Application.Interfaces;

/// <summary>
/// Puerto de almacenamiento físico. Hoy es una carpeta montada como volumen (RF-DOC-05);
/// mañana podría ser un bucket en la nube sin tocar el servicio.
/// </summary>
public interface IAlmacenamientoArchivos
{
    Task GuardarAsync(string nombreAlmacenado, Stream contenido, CancellationToken cancellationToken = default);

    /// <summary>Abre el archivo para lectura. Lanza <see cref="FileNotFoundException"/> si no existe.</summary>
    Task<Stream> AbrirLecturaAsync(string nombreAlmacenado, CancellationToken cancellationToken = default);

    /// <summary>Solo para deshacer una subida cuyo registro no se pudo guardar. No se usa al eliminar (RF-DOC-04).</summary>
    Task DescartarAsync(string nombreAlmacenado, CancellationToken cancellationToken = default);
}
