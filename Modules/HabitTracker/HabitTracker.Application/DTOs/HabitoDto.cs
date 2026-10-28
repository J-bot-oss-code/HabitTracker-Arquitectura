using HabitTracker.Domain.Entities;

namespace HabitTracker.Application.DTOs;

public sealed record HabitoDto(
    Guid Id,
    string Nombre,
    Periodicidad Periodicidad,
    decimal CantidadObjetivo,
    string Unidad,
    EstadoHabito Estado,
    bool CumpleMetaFinal,
    DateTime FechaInicioCiclo,
    IReadOnlyList<RegistroAvanceDto> Registros);

public sealed record RegistroAvanceDto(
    Guid Id,
    decimal Cantidad,
    DateTime Fecha,
    string Unidad);