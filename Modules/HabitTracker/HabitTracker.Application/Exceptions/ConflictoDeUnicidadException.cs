namespace HabitTracker.Application.Exceptions;

/// <summary>
/// Una restricción única de la base de datos se violó al guardar (por ejemplo, dos peticiones simultáneas que querían crear
/// el mismo plan). Application no conoce a EF: lo traduce <c>HabitTrackerUnitOfWork</c>. El servicio decide qué respuesta dar.
/// </summary>
public sealed class ConflictoDeUnicidadException(Exception? causa = null)
    : Exception("Ya existe un registro igual.", causa);
