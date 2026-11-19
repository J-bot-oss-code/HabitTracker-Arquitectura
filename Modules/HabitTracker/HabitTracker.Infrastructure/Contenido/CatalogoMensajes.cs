using System.Text.Json;
using System.Text.Json.Serialization;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Motivacion;

namespace HabitTracker.Infrastructure.Contenido;

/// <summary>
/// Lee <c>mensajes-motivacion.json</c> (recurso embebido). Es estricto, como el del catálogo de planes: un campo
/// desconocido, una categoría inválida, un texto vacío o de más de 160 caracteres, un duplicado o una categoría con menos de 2
/// mensajes (con uno solo el mismo texto saldría dos días seguidos) lanzan una excepción. El contenido se valida en una
/// prueba y al arrancar, no ante la persona.
/// </summary>
public static class LectorMensajesMotivacion
{
    public const string Recurso = "HabitTracker.Infrastructure.Contenido.mensajes-motivacion.json";
    public const int MinimoPorCategoria = 2;

    private static readonly JsonSerializerOptions Opciones = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) },
    };

    public static IReadOnlyList<MensajeMotivacion> Leer()
    {
        using var flujo = typeof(LectorMensajesMotivacion).Assembly.GetManifestResourceStream(Recurso)
            ?? throw new InvalidOperationException($"No se encontró el recurso embebido {Recurso}.");
        return Leer(flujo);
    }

    public static IReadOnlyList<MensajeMotivacion> Leer(Stream json)
    {
        var archivo = JsonSerializer.Deserialize<ArchivoJson>(json, Opciones)
            ?? throw new InvalidOperationException("El archivo de mensajes está vacío.");

        var mensajes = archivo.Mensajes.Select(m =>
        {
            var texto = m.Texto.Trim();
            if (texto.Length == 0 || texto.Length > SelectorMensajesMotivacion.LongitudMaxima)
                throw new InvalidOperationException(
                    $"Un mensaje de {m.Categoria} debe tener de 1 a {SelectorMensajesMotivacion.LongitudMaxima} caracteres: «{texto}».");
            return new MensajeMotivacion(m.Categoria, texto);
        }).ToList();

        if (mensajes.Select(m => m.Texto).Distinct(StringComparer.Ordinal).Count() != mensajes.Count)
            throw new InvalidOperationException("Hay mensajes de motivación repetidos.");

        foreach (var categoria in Enum.GetValues<CategoriaMensaje>())
            if (mensajes.Count(m => m.Categoria == categoria) < MinimoPorCategoria)
                throw new InvalidOperationException($"La categoría {categoria} necesita al menos {MinimoPorCategoria} mensajes.");

        return mensajes;
    }

    private sealed class ArchivoJson
    {
        public required int Version { get; init; }
        public required List<MensajeJson> Mensajes { get; init; }
    }

    private sealed class MensajeJson
    {
        public required CategoriaMensaje Categoria { get; init; }
        public required string Texto { get; init; }
    }
}

/// <summary>Los mensajes, leídos una sola vez. Si el archivo está mal, la API no arranca.</summary>
public sealed class CatalogoMensajes : ICatalogoMensajes
{
    public CatalogoMensajes() => Todos = LectorMensajesMotivacion.Leer();

    public IReadOnlyList<MensajeMotivacion> Todos { get; }
}
