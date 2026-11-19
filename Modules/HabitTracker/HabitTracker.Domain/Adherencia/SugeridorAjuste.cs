using HabitTracker.Domain.Planes;

namespace HabitTracker.Domain.Adherencia;

public enum TipoAjuste
{
    /// <summary>7 días en que la versión mínima cuenta como completa.</summary>
    ModoSuave,
    /// <summary>+1 semana al plan (máximo 2 veces).</summary>
    Extender,
    /// <summary>Pausar el plan con un motivo.</summary>
    Pausar,
}

/// <summary>Lo que se le ofrece a la persona. Nunca se aplica solo: es ella quien decide (P3).</summary>
public sealed record SugerenciaAjuste(IReadOnlyList<TipoAjuste> Opciones, int DiasSinRegistro);

/// <summary>
/// Cuándo ofrecer ajustar el plan (servicio de dominio puro). Con 4 o más días sin registro en los últimos 7 sugiere el
/// modo suave, extender el plan o pausarlo con un motivo. Solo propone: aplicar un ajuste es un paso aparte.
/// </summary>
public static class SugeridorAjuste
{
    public const int DiasSinRegistroParaSugerir = 4;

    public static SugerenciaAjuste? Sugerir(ResultadoProgreso progreso, EstadoPlanUsuario estado, int extensiones, bool enModoSuave)
    {
        ArgumentNullException.ThrowIfNull(progreso);

        // Solo un plan activo se ajusta; uno en pausa, completado o cancelado no necesita sugerencias.
        if (estado != EstadoPlanUsuario.Activo || progreso.DiasSinRegistroUltimos7 < DiasSinRegistroParaSugerir)
            return null;

        var opciones = new List<TipoAjuste>();
        if (!enModoSuave)
            opciones.Add(TipoAjuste.ModoSuave);
        if (extensiones < PlanUsuario.MaximoExtensiones)
            opciones.Add(TipoAjuste.Extender);
        opciones.Add(TipoAjuste.Pausar);

        return new SugerenciaAjuste(opciones, progreso.DiasSinRegistroUltimos7);
    }
}
