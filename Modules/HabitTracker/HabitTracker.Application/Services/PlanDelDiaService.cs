using System.Text.RegularExpressions;
using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Servicios;

namespace HabitTracker.Application.Services;

/// <summary>
/// Operación de negocio que usa datos de la API externa de clima: combina los hábitos ACTIVOS del usuario con el
/// clima de su ciudad y aplica las reglas de <see cref="RecomendadorClima"/>. No reenvía la respuesta del proveedor.
/// </summary>
public sealed partial class PlanDelDiaService(
    IHabitoRepository habitoRepository,
    IServicioClima servicioClima) : IPlanDelDiaService
{
    public const int LongitudMaximaCiudad = 100;

    // Letras (con acentos), espacios y los signos de un nombre de ciudad: "Santo Domingo", "São Paulo", "Saint-Denis".
    [GeneratedRegex(@"^[\p{L}\p{M}\s,.'\-]+$")]
    private static partial Regex FormatoCiudad();

    public async Task<PlanDelDiaDto> ObtenerAsync(Guid usuarioId, string ciudad, CancellationToken cancellationToken = default)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new UnauthorizedAccessException("Identidad de usuario inválida.");
        }

        // RD-07: la entrada externa se valida ANTES de consultar la base de datos o al proveedor.
        var ciudadLimpia = ciudad?.Trim() ?? string.Empty;
        if (ciudadLimpia.Length == 0)
        {
            throw new ArgumentException("La ciudad es obligatoria.", nameof(ciudad));
        }

        if (ciudadLimpia.Length > LongitudMaximaCiudad || !FormatoCiudad().IsMatch(ciudadLimpia))
        {
            throw new ArgumentException("La ciudad indicada no es válida.", nameof(ciudad));
        }

        var habitos = await habitoRepository.GetByUsuarioIdAsync(usuarioId, cancellationToken);
        var activos = habitos.Where(h => h.Estado == EstadoHabito.Activo).ToList();

        var clima = await servicioClima.ObtenerAsync(ciudadLimpia, cancellationToken);

        var lugar = RecomendadorClima.SugerirLugar(clima).ToString();
        var motivo = RecomendadorClima.ExplicarSugerencia(clima);

        var recomendaciones = activos
            .Select(h => new RecomendacionHabitoDto(h.Id, h.Nombre, lugar, motivo))
            .ToList();

        return new PlanDelDiaDto(
            ciudadLimpia,
            new ClimaDto(clima.TemperaturaC, clima.Precipitacion, clima.Descripcion),
            RecomendadorClima.AguaAdicionalMl(clima),
            recomendaciones);
    }
}
