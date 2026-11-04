namespace DocumentManager.Application.Exceptions;

/// <summary>El documento no existe, está eliminado o su archivo no está en el almacenamiento. Se traduce a 404.</summary>
public sealed class DocumentoNoEncontradoException() : Exception("El documento no existe.");

/// <summary>
/// El archivo no cumple las reglas de subida (ausente, vacío, demasiado grande o de un tipo no permitido).
/// Hereda de <see cref="ArgumentException"/>; su mensaje es seguro de mostrar al cliente (RD-07, RD-08).
/// </summary>
public sealed class ArchivoInvalidoException(string message) : ArgumentException(message);
