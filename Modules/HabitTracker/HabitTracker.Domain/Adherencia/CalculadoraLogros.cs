namespace HabitTracker.Domain.Adherencia;

/// <summary>Logros simbólicos, por conducta y nunca por el cuerpo (P4). Claves cerradas y estables.</summary>
public enum ClaveLogro
{
    PrimerAvance,
    PrimeraSemana,
    VeinticincoAvances,
    Constancia21,
    Constancia30,
    MetaCumplida,
    Regreso,
    MitadDelPlan,
    PlanCompletado,
}

/// <summary>
/// Lo único que la calculadora de logros recibe: contadores de conducta. No hay un solo parámetro de peso, medidas ni
/// IMC, y una prueba por reflexión lo comprueba (P4).
/// </summary>
public sealed record EntradaLogros(
    int AvancesRegistrados,
    int HabitosCompletados,
    int RachaMasLarga,
    bool HuboRegreso,
    bool MitadDelPlanAlcanzada,
    bool PlanCompletado);

/// <summary>
/// Qué logros se ganaron (servicio de dominio puro). Devuelve solo los que todavía no se tenían: cada logro se obtiene y se
/// felicita una sola vez.
/// </summary>
public static class CalculadoraLogros
{
    public const int DiasDeLaPrimeraSemana = 7;
    public const int DiasDeConstanciaMedia = 21;
    public const int DiasDeConstanciaLarga = 30;
    public const int AvancesParaElLogroGrande = 25;

    public static IReadOnlyList<ClaveLogro> Evaluar(EntradaLogros entrada, IReadOnlySet<ClaveLogro> yaObtenidos)
    {
        ArgumentNullException.ThrowIfNull(entrada);
        ArgumentNullException.ThrowIfNull(yaObtenidos);

        var ganados = new List<ClaveLogro>();
        void Si(bool condicion, ClaveLogro clave)
        {
            if (condicion && !yaObtenidos.Contains(clave))
                ganados.Add(clave);
        }

        Si(entrada.AvancesRegistrados >= 1, ClaveLogro.PrimerAvance);
        Si(entrada.RachaMasLarga >= DiasDeLaPrimeraSemana, ClaveLogro.PrimeraSemana);
        Si(entrada.AvancesRegistrados >= AvancesParaElLogroGrande, ClaveLogro.VeinticincoAvances);
        Si(entrada.RachaMasLarga >= DiasDeConstanciaMedia, ClaveLogro.Constancia21);
        Si(entrada.RachaMasLarga >= DiasDeConstanciaLarga, ClaveLogro.Constancia30);
        Si(entrada.HabitosCompletados >= 1, ClaveLogro.MetaCumplida);
        Si(entrada.HuboRegreso, ClaveLogro.Regreso);
        Si(entrada.MitadDelPlanAlcanzada, ClaveLogro.MitadDelPlan);
        Si(entrada.PlanCompletado, ClaveLogro.PlanCompletado);
        return ganados;
    }
}

/// <summary>Un logro de una persona. Único por persona y clave. Se felicita una sola vez (<see cref="VistoEn"/>).</summary>
public sealed class LogroUsuario
{
    private LogroUsuario() { } // EF Core

    public LogroUsuario(Guid usuarioId, ClaveLogro clave, DateTime obtenidoEnUtc)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("La identidad de la persona no es válida.", nameof(usuarioId));
        if (!Enum.IsDefined(clave))
            throw new ArgumentException("La clave del logro no es válida.", nameof(clave));
        if (obtenidoEnUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(obtenidoEnUtc));

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Clave = clave;
        ObtenidoEn = obtenidoEnUtc;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public ClaveLogro Clave { get; private set; }
    public DateTime ObtenidoEn { get; private set; }
    /// <summary>Nulo mientras la persona no lo haya visto celebrado.</summary>
    public DateTime? VistoEn { get; private set; }

    public void MarcarVisto(DateTime ahoraUtc) => VistoEn ??= ahoraUtc;
}
