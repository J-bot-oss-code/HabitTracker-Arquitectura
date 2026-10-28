using HabitTracker.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace HabitTracker.Application.DTOs;

public sealed record CrearHabitoCommand(
    [Required, StringLength(200, MinimumLength = 1)] string Nombre,
    [Required] Periodicidad Periodicidad,
    [Range(0.01, double.MaxValue)] decimal CantidadObjetivo,
    [Required, StringLength(50, MinimumLength = 1)] string Unidad,
    [Required] Guid MetaId);