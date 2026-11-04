using DocumentManager.Application.Interfaces;

namespace DocumentManager.Infrastructure.Storage;

/// <summary>
/// Guarda los archivos en una carpeta. En el contenedor esa carpeta es un volumen con nombre
/// (RF-DOC-05), así que sobrevive a destruir y recrear el contenedor.
/// </summary>
public sealed class AlmacenamientoArchivosLocal : IAlmacenamientoArchivos
{
    private readonly string _raiz;

    public AlmacenamientoArchivosLocal(string rutaRaiz)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rutaRaiz);

        // Sin la barra final: "/app/data/documentos/" y "/app/data/documentos" deben ser la misma raíz.
        _raiz = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rutaRaiz));
        Directory.CreateDirectory(_raiz);
    }

    public async Task GuardarAsync(string nombreAlmacenado, Stream contenido, CancellationToken cancellationToken = default)
    {
        var ruta = ResolverRuta(nombreAlmacenado);

        var creado = false;
        try
        {
            // CreateNew: si ya existe un archivo con ese nombre se falla en lugar de sobrescribirlo.
            await using var destino = new FileStream(ruta, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            creado = true;
            await contenido.CopyToAsync(destino, cancellationToken);
        }
        catch
        {
            // Una copia interrumpida (disco lleno, cancelación) no debe dejar un archivo parcial.
            // Solo se borra si lo creamos nosotros: si CreateNew falló, el archivo existente no es nuestro.
            if (creado && File.Exists(ruta))
            {
                File.Delete(ruta);
            }

            throw;
        }
    }

    public Task<Stream> AbrirLecturaAsync(string nombreAlmacenado, CancellationToken cancellationToken = default)
    {
        var ruta = ResolverRuta(nombreAlmacenado);

        try
        {
            Stream flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult(flujo);
        }
        catch (FileNotFoundException)
        {
            throw new FileNotFoundException("El archivo no está en el almacenamiento.");
        }
        catch (DirectoryNotFoundException)
        {
            throw new FileNotFoundException("El archivo no está en el almacenamiento.");
        }
    }

    public Task DescartarAsync(string nombreAlmacenado, CancellationToken cancellationToken = default)
    {
        var ruta = ResolverRuta(nombreAlmacenado);
        if (File.Exists(ruta))
        {
            File.Delete(ruta);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Convierte el nombre almacenado en una ruta y comprueba que quede DENTRO de la raíz.
    /// Defensa en profundidad: el dominio ya genera un GUID, pero esta capa no confía en ello.
    /// </summary>
    private string ResolverRuta(string nombreAlmacenado)
    {
        // Se rechazan "/" y "\" de forma explícita en todos los sistemas: en Linux "\" no es separador
        // y Path.GetFileName lo dejaría pasar como parte del nombre.
        if (string.IsNullOrWhiteSpace(nombreAlmacenado)
            || nombreAlmacenado.IndexOfAny(['/', '\\']) >= 0
            || nombreAlmacenado.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
            || nombreAlmacenado != Path.GetFileName(nombreAlmacenado))
        {
            throw new ArgumentException("Nombre de archivo no válido.", nameof(nombreAlmacenado));
        }

        var ruta = Path.GetFullPath(Path.Combine(_raiz, nombreAlmacenado));
        if (!ruta.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException("Nombre de archivo no válido.", nameof(nombreAlmacenado));
        }

        return ruta;
    }
}
