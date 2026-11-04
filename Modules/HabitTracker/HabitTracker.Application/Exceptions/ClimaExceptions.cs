namespace HabitTracker.Application.Exceptions;

/// <summary>
/// El clima no se pudo obtener (falta la clave, el proveedor no responde o rechazó la petición). Se traduce a
/// 503. El mensaje es genérico a propósito: nunca lleva la clave, la URL ni detalles del proveedor (RD-08).
/// </summary>
public sealed class ServicioClimaNoDisponibleException()
    : Exception("El servicio de clima no está disponible en este momento.");

/// <summary>El proveedor de clima no conoce la ciudad indicada. Se traduce a 404.</summary>
public sealed class CiudadNoEncontradaException()
    : Exception("No se encontró la ciudad indicada.");
