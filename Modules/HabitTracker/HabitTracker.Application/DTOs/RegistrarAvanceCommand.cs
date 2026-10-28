using System.ComponentModel.DataAnnotations;

namespace HabitTracker.Application.DTOs;

public sealed record RegistrarAvanceCommand(
    [Required] Guid HabitoId,
    [Range(0.01, double.MaxValue)] decimal Cantidad,
    [Required] DateTime FechaUtc);