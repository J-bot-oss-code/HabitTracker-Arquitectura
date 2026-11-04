namespace HabitTracker.Application.DTOs;

public sealed record ClimaDto(decimal TemperaturaC, bool Precipitacion, string Descripcion);

public sealed record RecomendacionHabitoDto(Guid HabitoId, string Nombre, string LugarSugerido, string Motivo);

/// <summary>Plan del día: el clima de la ciudad y qué conviene hacer con cada hábito activo.</summary>
public sealed record PlanDelDiaDto(
    string Ciudad,
    ClimaDto Clima,
    int AguaAdicionalMl,
    IReadOnlyList<RecomendacionHabitoDto> Habitos);
