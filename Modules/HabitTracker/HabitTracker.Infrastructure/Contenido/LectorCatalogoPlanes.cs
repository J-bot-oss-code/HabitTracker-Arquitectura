using System.Text.Json;
using System.Text.Json.Serialization;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Infrastructure.Contenido;

/// <summary>
/// Lee <c>catalogo-planes.json</c> (recurso embebido) y lo traduce al dominio. Es estricto: un campo desconocido, un
/// enum inválido, un slug repetido o un plan que no cumple las invariantes del dominio lanzan una excepción, así que un
/// error de contenido rompe la prueba de contenido y el arranque antes de llegar a nadie.
/// </summary>
public static class LectorCatalogoPlanes
{
    public const string Recurso = "HabitTracker.Infrastructure.Contenido.catalogo-planes.json";

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static IReadOnlyList<ContenidoPlan> Leer()
    {
        using var flujo = typeof(LectorCatalogoPlanes).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"No se encontró el recurso embebido {Recurso}.");
        return Leer(flujo);
    }

    public static IReadOnlyList<ContenidoPlan> Leer(Stream json)
    {
        var catalogo = JsonSerializer.Deserialize<CatalogoJson>(json, Opciones)
            ?? throw new InvalidOperationException("El catálogo de planes está vacío.");
        if (catalogo.Planes.Count == 0)
            throw new InvalidOperationException("El catálogo de planes no tiene planes.");

        var repetidos = catalogo.Planes.GroupBy(p => p.Slug, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (repetidos.Count > 0)
            throw new InvalidOperationException($"Slugs repetidos en el catálogo: {string.Join(", ", repetidos)}.");

        var contenidos = catalogo.Planes.Select(ADominio).ToList();
        foreach (var contenido in contenidos)
            _ = new PlanPlantilla(contenido); // valida las invariantes del dominio
        return contenidos;
    }

    private static ContenidoPlan ADominio(PlanJson p) => new(
        p.Slug, p.Nombre, p.Resumen, p.Descripcion, p.Area, p.Nivel, p.Intensidad, p.Sexo, p.EdadMinima, p.EdadMaxima,
        p.Semanas, p.PerfilesLaborales, p.Lugar, p.Impacto, p.ZonasCorporales, p.Contraindicaciones, p.Alergenos,
        p.Beneficios, p.AvisoSeguridad,
        p.GuiaSemanal.Select(g => new SemanaGuia(g.Semana, g.Texto, g.TipExtra)).ToList(),
        p.RequiereCuestionario, p.VersionContenido,
        p.Tareas.Select(t => new TareaPlantilla(
            t.Orden, t.Nombre, t.Instrucciones, t.Periodicidad, t.Cantidad, t.Unidad, t.VersionMinima, t.AnclaSugerida)).ToList());

    private sealed class CatalogoJson
    {
        public required int Version { get; init; }
        public required List<PlanJson> Planes { get; init; }
    }

    private sealed class PlanJson
    {
        public required string Slug { get; init; }
        public required string Nombre { get; init; }
        public required string Resumen { get; init; }
        public required string Descripcion { get; init; }
        public required AreaPlan Area { get; init; }
        public required NivelPlan Nivel { get; init; }
        public required IntensidadPlan Intensidad { get; init; }
        public SexoPlan Sexo { get; init; } = SexoPlan.Todos;
        public int EdadMinima { get; init; } = PlanPlantilla.EdadMinimaPermitida;
        public int EdadMaxima { get; init; } = PlanPlantilla.EdadMaximaPermitida;
        public required int Semanas { get; init; }
        public List<PerfilLaboral> PerfilesLaborales { get; init; } = [];
        public Lugar? Lugar { get; init; }
        public bool Impacto { get; init; }
        public List<ZonaCorporal> ZonasCorporales { get; init; } = [];
        public List<CondicionSalud> Contraindicaciones { get; init; } = [];
        public List<Alergia> Alergenos { get; init; } = [];
        public required List<string> Beneficios { get; init; }
        public required string AvisoSeguridad { get; init; }
        public List<SemanaJson> GuiaSemanal { get; init; } = [];
        public required bool RequiereCuestionario { get; init; }
        public int VersionContenido { get; init; } = 1;
        public required List<TareaJson> Tareas { get; init; }
    }

    private sealed class SemanaJson
    {
        public required int Semana { get; init; }
        public required string Texto { get; init; }
        public string? TipExtra { get; init; }
    }

    private sealed class TareaJson
    {
        public required int Orden { get; init; }
        public required string Nombre { get; init; }
        public required string Instrucciones { get; init; }
        public required Periodicidad Periodicidad { get; init; }
        public required decimal Cantidad { get; init; }
        public required string Unidad { get; init; }
        public required string VersionMinima { get; init; }
        public required Ancla AnclaSugerida { get; init; }
    }
}
