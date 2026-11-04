using HabitTracker.Domain.Servicios;

namespace HabitTracker.Application.Interfaces;

/// <summary>
/// Puerto hacia un proveedor de clima externo. La aplicación no sabe que detrás está OpenWeatherMap:
/// se puede cambiar de proveedor, o usar un doble en las pruebas, sin tocar el servicio (RD-01, RD-12).
/// </summary>
public interface IServicioClima
{
    /// <exception cref="Exceptions.ServicioClimaNoDisponibleException">Sin clave, sin red, con error del proveedor o con timeout.</exception>
    /// <exception cref="Exceptions.CiudadNoEncontradaException">El proveedor no conoce la ciudad.</exception>
    Task<CondicionClimatica> ObtenerAsync(string ciudad, CancellationToken cancellationToken = default);
}
