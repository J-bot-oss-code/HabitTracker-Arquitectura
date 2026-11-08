namespace HabitTracker.Domain.Excepciones;

/// <summary>
/// Base de los rechazos del módulo de negocio con un código estable (por ejemplo <c>fecha_futura</c>). El middleware de la
/// API la traduce a <c>ProblemDetails</c> con la extensión <c>codigo</c>, para que el cliente no dependa del texto del
/// mensaje (RD-08). Hereda de <see cref="InvalidOperationException"/>, como <c>ControlAccesoException</c>.
/// </summary>
public abstract class HabitTrackerException : InvalidOperationException
{
    protected HabitTrackerException(string codigo, string mensaje) : base(mensaje)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El código del error es obligatorio.", nameof(codigo));

        Codigo = codigo;
    }

    /// <summary>Código estable en snake_case que viaja en <c>ProblemDetails.codigo</c>.</summary>
    public string Codigo { get; }
}

/// <summary>El recurso pedido no existe (HTTP 404). Ejemplo: <c>habito_no_encontrado</c>.</summary>
public sealed class RecursoNoEncontradoException(string codigo, string mensaje) : HabitTrackerException(codigo, mensaje)
{
    public const string HabitoNoEncontrado = "habito_no_encontrado";
}

/// <summary>Una regla de negocio rechaza la operación (HTTP 400). Ejemplos: <c>fecha_futura</c>, <c>meta_no_encontrada</c>.</summary>
public sealed class ReglaNegocioException(string codigo, string mensaje) : HabitTrackerException(codigo, mensaje)
{
    public const string FechaFutura = "fecha_futura";
    public const string MetaNoEncontrada = "meta_no_encontrada";
}
