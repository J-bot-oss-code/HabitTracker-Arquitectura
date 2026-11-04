namespace HabitTracker.Domain.Servicios;

/// <summary>Condiciones del clima que importan para planificar hábitos. No sabe de dónde vinieron los datos.</summary>
public sealed record CondicionClimatica(decimal TemperaturaC, bool Precipitacion, string Descripcion);

public enum LugarSugerido
{
    AireLibre,
    BajoTecho
}

/// <summary>
/// Reglas de negocio que convierten el clima en recomendaciones para los hábitos del día. Es una clase pura:
/// sin HTTP, sin base de datos y sin reloj, así que se prueba con solo llamar a sus métodos (RD-12).
/// Aquí vive la lógica que USA los datos de la API externa; por eso la operación no es un simple reenvío.
/// </summary>
public static class RecomendadorClima
{
    public const decimal TemperaturaMinimaAireLibre = 5m;
    public const decimal TemperaturaMaximaAireLibre = 35m;

    public static LugarSugerido SugerirLugar(CondicionClimatica clima)
    {
        ArgumentNullException.ThrowIfNull(clima);

        if (clima.Precipitacion
            || clima.TemperaturaC < TemperaturaMinimaAireLibre
            || clima.TemperaturaC > TemperaturaMaximaAireLibre)
        {
            return LugarSugerido.BajoTecho;
        }

        return LugarSugerido.AireLibre;
    }

    /// <summary>Explica en una frase por qué se sugiere ese lugar.</summary>
    public static string ExplicarSugerencia(CondicionClimatica clima)
    {
        ArgumentNullException.ThrowIfNull(clima);

        if (clima.Precipitacion)
        {
            return "Hay precipitación: conviene hacerlo bajo techo.";
        }

        if (clima.TemperaturaC > TemperaturaMaximaAireLibre)
        {
            return "Hace demasiado calor para el exterior: conviene hacerlo bajo techo.";
        }

        if (clima.TemperaturaC < TemperaturaMinimaAireLibre)
        {
            return "Hace demasiado frío para el exterior: conviene hacerlo bajo techo.";
        }

        return "El clima es favorable: puedes hacerlo al aire libre.";
    }

    /// <summary>Mililitros de agua extra recomendados según el calor (0 si no hace falta).</summary>
    public static int AguaAdicionalMl(CondicionClimatica clima)
    {
        ArgumentNullException.ThrowIfNull(clima);

        return clima.TemperaturaC switch
        {
            > 35m => 1000,
            >= 30m => 500,
            >= 25m => 250,
            _ => 0
        };
    }
}
