using DocumentManager.Application.DTOs;
using DocumentManager.Application.Exceptions;
using DocumentManager.Application.Interfaces;
using DocumentManager.Domain.Entities;
using DocumentManager.Domain.Results;

namespace DocumentManager.Application.Services;

public sealed class DocumentoService(
    IDocumentoRepository repositorio,
    IAlmacenamientoArchivos almacenamiento) : IDocumentoService
{
    public const long TamanoMaximoBytes = 10 * 1024 * 1024;

    // Lista blanca extensión -> tipo MIME. El tipo que se guarda y se sirve sale de aquí, nunca del cliente:
    // si no, un .txt declarado como text/html se serviría como HTML (XSS almacenado).
    private static readonly Dictionary<string, string> TiposPermitidos = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".txt"] = "text/plain",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public async Task<int> PurgarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("El usuario es obligatorio.", nameof(usuarioId));
        }

        // Primero los archivos y después los registros: si falla a mitad, los registros que quedan permiten reintentar
        // (descartar un archivo que ya no está no es un error). Al revés, quedarían archivos huérfanos sin registro.
        var documentos = await repositorio.ListarTodosDeUsuarioAsync(usuarioId, cancellationToken);
        foreach (var documento in documentos)
        {
            await almacenamiento.DescartarAsync(documento.NombreAlmacenado, cancellationToken);
        }

        return await repositorio.PurgarDeUsuarioAsync(usuarioId, cancellationToken);
    }

    public async Task<DocumentoDto> SubirAsync(SubirDocumentoCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // RD-07: toda entrada externa se valida antes de tocar disco o base de datos.
        if (command.Contenido is null)
        {
            throw new ArchivoInvalidoException("Debe adjuntar un archivo que no esté vacío.");
        }

        var nombre = LimpiarNombre(command.NombreArchivo);
        if (nombre is null || !TiposPermitidos.TryGetValue(Path.GetExtension(nombre), out var tipo))
        {
            throw new ArchivoInvalidoException("El tipo de archivo no está permitido.");
        }

        // Se mide el tamaño REAL leyendo los bytes, con un tope. Así nadie declara 1 byte y sube 1 GB.
        var contenido = await LeerConTopeAsync(command.Contenido, cancellationToken);
        if (contenido.Length == 0)
        {
            throw new ArchivoInvalidoException("Debe adjuntar un archivo que no esté vacío.");
        }

        // El constructor del dominio valida longitudes y genera el nombre almacenado (GUID).
        Documento documento;
        try
        {
            documento = new Documento(nombre, tipo, contenido.Length, command.UsuarioId, command.CorreoUsuario);
        }
        catch (ArgumentException)
        {
            throw new ArchivoInvalidoException("Los datos del archivo no son válidos.");
        }

        try
        {
            await almacenamiento.GuardarAsync(documento.NombreAlmacenado, contenido, cancellationToken);
            await repositorio.AddAsync(documento, cancellationToken);
        }
        catch
        {
            // Si algo falla a mitad, no dejamos un archivo (parcial o completo) huérfano en el volumen.
            await IntentarDescartarAsync(documento.NombreAlmacenado);
            throw;
        }

        return ADto(documento);
    }

    public async Task<IReadOnlyList<DocumentoDto>> ListarAsync(Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default)
    {
        // RF-DOC-02: el Administrador ve todos los no eliminados; el Estándar solo los suyos.
        var documentos = await repositorio.ListarNoEliminadosAsync(esAdministrador ? null : usuarioId, cancellationToken);
        return documentos.Select(ADto).ToList();
    }

    public async Task<DocumentoDto> ObtenerAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default)
    {
        var documento = await ObtenerAutorizadoAsync(id, usuarioId, esAdministrador, cancellationToken);
        return ADto(documento);
    }

    public async Task<ArchivoDescargaDto> DescargarAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default)
    {
        var documento = await ObtenerAutorizadoAsync(id, usuarioId, esAdministrador, cancellationToken);

        try
        {
            var contenido = await almacenamiento.AbrirLecturaAsync(documento.NombreAlmacenado, cancellationToken);
            return new ArchivoDescargaDto(contenido, documento.TipoContenido, documento.NombreOriginal);
        }
        catch (FileNotFoundException)
        {
            throw new DocumentoNoEncontradoException();
        }
    }

    public async Task EliminarAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken = default)
    {
        var documento = await ObtenerAutorizadoAsync(id, usuarioId, esAdministrador, cancellationToken);

        // RF-DOC-04: solo se marca; el registro y el archivo permanecen.
        var resultado = documento.Eliminar(DateTime.UtcNow);
        if (!resultado.IsSuccess)
        {
            if (resultado.Error == DomainError.DocumentoYaEliminado)
            {
                throw new DocumentoNoEncontradoException();
            }

            throw new InvalidOperationException("No se pudo eliminar el documento.");
        }

        await repositorio.UpdateAsync(documento, cancellationToken);
    }

    /// <summary>
    /// Carga el documento y aplica las dos reglas comunes: un eliminado cuenta como inexistente (404)
    /// y solo el dueño o un Administrador pasa (403). Un único punto para consulta, descarga y baja (RD-06).
    /// </summary>
    private async Task<Documento> ObtenerAutorizadoAsync(Guid id, Guid usuarioId, bool esAdministrador, CancellationToken cancellationToken)
    {
        var documento = await repositorio.ObtenerPorIdAsync(id, cancellationToken);
        if (documento is null || documento.Eliminado)
        {
            throw new DocumentoNoEncontradoException();
        }

        if (!documento.PuedeAcceder(usuarioId, esAdministrador))
        {
            throw new UnauthorizedAccessException("No tiene permiso para acceder a este documento.");
        }

        return documento;
    }

    /// <summary>
    /// Se queda con el último segmento del nombre. Se reemplaza "\" por "/" ANTES de Path.GetFileName porque en Linux
    /// (el contenedor) "\" no es separador y "..\\x.txt" quedaría entero. Rechaza caracteres de control.
    /// </summary>
    private static string? LimpiarNombre(string? nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo))
        {
            return null;
        }

        var nombre = Path.GetFileName(nombreArchivo.Trim().Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(nombre) || nombre.Any(char.IsControl))
        {
            return null;
        }

        return nombre;
    }

    private static async Task<MemoryStream> LeerConTopeAsync(Stream origen, CancellationToken cancellationToken)
    {
        var destino = new MemoryStream();
        var buffer = new byte[81920];
        int leidos;

        while ((leidos = await origen.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (destino.Length + leidos > TamanoMaximoBytes)
            {
                throw new ArchivoInvalidoException("El archivo supera el tamaño máximo permitido de 10 MB.");
            }

            destino.Write(buffer, 0, leidos);
        }

        destino.Position = 0;
        return destino;
    }

    private async Task IntentarDescartarAsync(string nombreAlmacenado)
    {
        try
        {
            await almacenamiento.DescartarAsync(nombreAlmacenado, CancellationToken.None);
        }
        catch
        {
            // La excepción original es la que importa; un fallo al limpiar no debe enmascararla.
        }
    }

    private static DocumentoDto ADto(Documento d) =>
        new(d.Id, d.NombreOriginal, d.TipoContenido, d.TamanoBytes, d.FechaSubidaUtc, d.UsuarioId, d.CorreoUsuario);
}
