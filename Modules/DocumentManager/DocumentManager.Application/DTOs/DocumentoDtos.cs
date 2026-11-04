namespace DocumentManager.Application.DTOs;

/// <summary>
/// Datos de entrada de la subida. La identidad la pone el servidor desde los claims. El tamaño y el tipo
/// NO viajan aquí a propósito: el cliente podría mentir en ambos, así que el servicio los calcula (RD-07).
/// </summary>
public sealed record SubirDocumentoCommand(
    Guid UsuarioId,
    string CorreoUsuario,
    string NombreArchivo,
    Stream? Contenido);

/// <summary>Metadatos que se devuelven al cliente (nunca la ruta física ni el nombre almacenado).</summary>
public sealed record DocumentoDto(
    Guid Id,
    string Nombre,
    string Tipo,
    long TamanoBytes,
    DateTime FechaSubidaUtc,
    Guid UsuarioId,
    string CorreoUsuario);

public sealed record ArchivoDescargaDto(Stream Contenido, string TipoContenido, string NombreArchivo);
