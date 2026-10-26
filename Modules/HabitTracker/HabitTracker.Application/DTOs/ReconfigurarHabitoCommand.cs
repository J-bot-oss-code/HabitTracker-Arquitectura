using HabitTracker.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace HabitTracker.Application.DTOs;

public sealed record ReconfigurarHabitoCommand(
    [Required] Guid HabitoId,
    [Required] Periodicidad Periodicidad,
    [Range(0.01, double.MaxValue)] decimal CantidadObjetivo,
    [Required, StringLength(50, MinimumLength = 1)] string Unidad);