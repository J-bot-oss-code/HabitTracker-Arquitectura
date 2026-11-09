using HabitTracker.Application.DTOs;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Services;

public interface IPlanesService
{
    /// <summary>Catálogo público de planes activos, con filtros opcionales. Orden estable: Básicos primero y por nombre.</summary>
    Task<IReadOnlyList<PlanResumenDto>> ListarAsync(FiltrosPlanes filtros, CancellationToken cancellationToken = default);

    /// <summary>Detalle de un plan activo; 404 <c>plan_no_encontrado</c> si no existe o se retiró.</summary>
    Task<PlanDetalleDto> ObtenerAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recomendados para la persona, con motivos, y los no aptos con su razón. 404 <c>perfil_no_encontrado</c> antes del
    /// onboarding. Sin el consentimiento <c>datosSalud</c> recomienda solo por la jornada y la edad.
    /// </summary>
    Task<RecomendacionesDto> RecomendarAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed class PlanesService(
    IPlanPlantillaRepository planes,
    IPerfilUsuarioRepository perfiles,
    ICuestionarioSaludRepository cuestionarios,
    IConsentimientoSalud consentimiento) : IPlanesService
{
    public async Task<IReadOnlyList<PlanResumenDto>> ListarAsync(FiltrosPlanes filtros, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtros);
        var activos = await planes.ListarActivosAsync(cancellationToken);
        return activos
            .Where(p => filtros.Area is null || p.Area == filtros.Area)
            .Where(p => filtros.Nivel is null || p.Nivel == filtros.Nivel)
            .Where(p => filtros.PerfilLaboral is not { } perfil
                || p.PerfilesLaborales.Count == 0
                || p.PerfilesLaborales.Contains(perfil))
            .OrderBy(p => p.Nivel)
            .ThenBy(p => p.Nombre, StringComparer.Ordinal)
            .Select(Resumen)
            .ToList();
    }

    public async Task<PlanDetalleDto> ObtenerAsync(string slug, CancellationToken cancellationToken = default)
    {
        var plan = string.IsNullOrWhiteSpace(slug) || slug.Length > PlanPlantilla.LongitudSlug
            ? null
            : await planes.ObtenerActivoPorSlugAsync(slug, cancellationToken);
        return plan is null
            ? throw new RecursoNoEncontradoException(RecursoNoEncontradoException.PlanNoEncontrado, "No encontramos ese plan.")
            : Detalle(plan);
    }

    public async Task<RecomendacionesDto> RecomendarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken)
            ?? throw new RecursoNoEncontradoException(RecursoNoEncontradoException.PerfilNoEncontrado, "Aún no completaste tu perfil.");
        var consiente = await consentimiento.TieneConsentimientoDatosSaludAsync(usuarioId, cancellationToken);
        // Sin consentimiento el cuestionario ni se lee: sus datos siguen guardados, pero no se usan.
        var cuestionario = consiente ? await cuestionarios.ObtenerAsync(usuarioId, cancellationToken) : null;

        var persona = SituacionPersona.Crear(perfil, cuestionario, consiente);
        var resultado = RecomendadorPlanes.Recomendar(await planes.ListarActivosAsync(cancellationToken), persona);

        return new RecomendacionesDto(
            resultado.Recomendados.Select(r => new PlanRecomendadoDto(Resumen(r.Plan), r.Motivos, r.Advertencias)).ToList(),
            resultado.NoAptos.Select(n => new PlanNoAptoDto(
                Resumen(n.Plan), n.Razones.Select(r => new RazonNoAptoDto(r.Motivo, r.Mensaje)).ToList())).ToList(),
            resultado.PersonalizadoConSalud);
    }

    internal static PlanResumenDto Resumen(PlanPlantilla p) => new(
        p.Slug, p.Nombre, p.Resumen, p.Area, p.Nivel, p.Intensidad, p.Semanas, p.Sexo, p.EdadMinima, p.EdadMaxima,
        p.PerfilesLaborales, p.Beneficios, p.RequiereCuestionario);

    internal static PlanDetalleDto Detalle(PlanPlantilla p) => new(
        p.Slug, p.Nombre, p.Resumen, p.Descripcion, p.Area, p.Nivel, p.Intensidad, p.Semanas, p.Sexo, p.EdadMinima,
        p.EdadMaxima, p.PerfilesLaborales, p.Lugar, p.Impacto, p.ZonasCorporales, p.Contraindicaciones, p.Alergenos,
        p.Beneficios, p.AvisoSeguridad, p.RequiereCuestionario,
        p.Tareas.Select(t => new TareaPlantillaDto(
            t.Orden, t.Nombre, t.Instrucciones, t.Periodicidad, t.Cantidad, t.Unidad, t.VersionMinima, t.AnclaSugerida)).ToList(),
        (p.GuiaSemanal ?? []).Select(g => new SemanaGuiaDto(g.Semana, g.Texto)).ToList()); // una columna NULL no da un 500
}
