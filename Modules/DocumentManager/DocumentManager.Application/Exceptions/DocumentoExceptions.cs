namespace DocumentManager.Application.Exceptions;

/// <summary>El documento no existe, está eliminado o su archivo no está en el almacenamiento. Se traduce a 404.</summary>
public sealed class DocumentoNoEncontradoException() : Exception("El documento no existe.");

/// <summary>
/// El archivo no cumple las reglas de subida (ausente, vacío, demasiado grande o de un tipo no permitido).
/// Hereda de <see cref="ArgumentException"/>; su mensaje es seguro de mostrar al cliente (RD-07, RD-08).
/// </summary>
public sealed class ArchivoInvalidoException(string message) : ArgumentException(message);

/// <summary>
/// El dueño no puede eliminar este documento todavía: otro módulo lo protege y hace falta un permiso. Se traduce a 409 con el código
/// <c>documento_requiere_permiso</c> (RD-08: no dice qué módulo ni por qué).
/// </summary>
public sealed class DocumentoProtegidoException() : InvalidOperationException("Este archivo está en uso: pide permiso para eliminarlo.");
