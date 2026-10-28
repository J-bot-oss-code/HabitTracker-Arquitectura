using HabitTracker.Domain.Entities;
using System.ComponentModel.DataAnnotations;

namespace HabitTracker.Application.DTOs;

public sealed record CambiarEstadoHabitoCommand(
    [Required] Guid HabitoId,
    [Required] EstadoHabito NuevoEstado);