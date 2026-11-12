using HabitTracker.Domain.Adherencia;

namespace HabitTracker.Domain.Motivacion;

public enum CategoriaMensaje
{
    Inicio,
    Constancia,
    /// <summary>La persona acaba de volver tras unos días sin registro.</summary>
    Recaida,
    Hito,
    Cierre,
    /// <summary>Lleva 2 días o más sin registro: una invitación, nunca un reproche.</summary>
    Regreso,
}

public sealed record MensajeMotivacion(CategoriaMensaje Categoria, string Texto);

/// <summary>El momento de la persona en su plan, reducido a lo que decide el tono del mensaje.</summary>
public sealed record SituacionMensaje(
    int DiasTranscurridos,
    int Racha,
    int DiasSinRegistroSeguidos,
    bool VolvioHoy,
    int SemanasTotales,
    bool PlanTerminado)
{
    public static SituacionMensaje Desde(ResultadoProgreso progreso, bool planTerminado)
    {
        ArgumentNullException.ThrowIfNull(progreso);
        return new SituacionMensaje(
            progreso.DiasTranscurridos,
            progreso.Racha,
            progreso.DiasSinRegistroSeguidos,
            progreso.Regresos.Contains(progreso.HoyLocal),
            progreso.SemanasTotales,
            planTerminado);
    }
}

/// <summary>
/// Elige el mensaje de motivación del día (servicio de dominio puro). Es determinista: el índice sale de un hash estable de
/// la persona más el número de día local, así que cada día toca un mensaje distinto del anterior (con 2 o más en la
/// categoría) y el mismo día da siempre el mismo. El tono depende del momento, y con 2 días sin registro o más es de
/// regreso, nunca de reproche (P3).
/// </summary>
public static class SelectorMensajesMotivacion
{
    public const int LongitudMaxima = 160;
    private static readonly int[] DiasDeRacha = [7, 21, 30];

    public static CategoriaMensaje Categorizar(SituacionMensaje s)
    {
        ArgumentNullException.ThrowIfNull(s);

        if (s.PlanTerminado)
            return CategoriaMensaje.Cierre;
        if (s.DiasSinRegistroSeguidos >= CalculadoraProgreso.DiasSeguidosQueCortan)
            return CategoriaMensaje.Regreso;
        if (s.VolvioHoy)
            return CategoriaMensaje.Recaida;
        if (DiasDeRacha.Contains(s.Racha) || EsLaMitadDelPlan(s))
            return CategoriaMensaje.Hito;
        return s.DiasTranscurridos <= 2 ? CategoriaMensaje.Inicio : CategoriaMensaje.Constancia;
    }

    /// <summary>El mensaje de la categoría para ese día, o nulo si el catálogo no tiene ninguno de ella.</summary>
    public static string? Elegir(IReadOnlyList<MensajeMotivacion> catalogo, CategoriaMensaje categoria, Guid usuarioId, DateOnly diaLocal)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        var textos = catalogo.Where(m => m.Categoria == categoria).Select(m => m.Texto).ToList();
        if (textos.Count == 0)
            return null;

        // ulong: sumar el día a un hash de 32 bits no debe desbordar justo donde dos días seguidos tendrían que diferir.
        var indice = (int)((HashEstable(usuarioId) + (ulong)diaLocal.DayNumber) % (ulong)textos.Count);
        return textos[indice];
    }

    private static bool EsLaMitadDelPlan(SituacionMensaje s) =>
        s.SemanasTotales > 0 && s.DiasTranscurridos == (s.SemanasTotales * 7 + 1) / 2;

    // FNV-1a sobre los bytes del Guid: estable entre procesos, a diferencia de string.GetHashCode().
    private static ulong HashEstable(Guid id)
    {
        uint hash = 2166136261;
        foreach (var b in id.ToByteArray())
            hash = (hash ^ b) * 16777619;
        return hash;
    }
}
