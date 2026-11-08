using HabitTracker.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace HabitTracker.Application.DTOs;

/// <summary>Datos para crear un hábito. <see cref="MetaId"/> es opcional: un hábito puede ir suelto, sin meta del catálogo.</summary>
public sealed record CrearHabitoCommand(
    [Required, StringLength(200, MinimumLength = 1)] string Nombre,
    [Required] Periodicidad Periodicidad,
    [Range(0.01, double.MaxValue)] decimal CantidadObjetivo,
    [Required, StringLength(50, MinimumLength = 1)] string Unidad,
    Guid? MetaId = null);
