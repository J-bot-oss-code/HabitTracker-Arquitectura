namespace HabitTracker.Domain.Notificaciones;

/// <summary>Cuántos envíos le tocan a la persona según cuánto lleva sin abrir la app. De más a menos.</summary>
public enum ModoResumen
{
    Diario,
    /// <summary>El resumen diario sigue, pero con el mensaje de regreso (P3: volver es parte del plan).</summary>
    DiarioConRegreso,
    Semanal,
    /// <summary>Sin resúmenes hasta que la persona vuelva.</summary>
    Detenido,
}

/// <param name="EnviarAvisoRetomar">El único «¿seguimos?»: solo cuando se llega al tope y todavía no se mandó.</param>
public sealed record DecisionCadencia(ModoResumen Modo, bool EnviarAvisoRetomar);

/// <summary>Días sin actividad en que cambia el modo. Viven en <c>OpcionesRecordatorios</c>; estos son los del plan.</summary>
public sealed record UmbralesCadencia(int DiasParaRegreso = 3, int DiasParaSemanal = 7, int DiasParaRetomar = 21)
{
    public void Validar()
    {
        if (DiasParaRegreso < 1 || DiasParaSemanal <= DiasParaRegreso || DiasParaRetomar <= DiasParaSemanal)
            throw new ArgumentException("Los umbrales deben ser positivos y crecientes.", nameof(DiasParaRegreso));
    }
}

/// <summary>
/// Decide el modo del resumen por cadencia de uso (dominio puro). <b>Solo reduce</b>: nunca reactiva un correo ni toca el
/// consentimiento, y con más días sin actividad nunca devuelve un modo con más envíos. Se mide con la última actividad, sin
/// píxeles de seguimiento.
/// </summary>
public static class CalculadoraCadencia
{
    /// <summary>Días locales completos desde la última actividad; sin actividad registrada no hay nada que reducir (0).</summary>
    public static int DiasSinActividad(DateTime? ultimaActividadUtc, TimeZoneInfo zona, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(zona);
        if (ahoraUtc.Kind != DateTimeKind.Utc || ultimaActividadUtc is { Kind: not DateTimeKind.Utc })
            throw new ArgumentException("Las fechas deben ser UTC.");
        if (ultimaActividadUtc is not { } ultima)
            return 0;

        var dias = (TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zona).Date - TimeZoneInfo.ConvertTimeFromUtc(ultima, zona).Date).Days;
        return Math.Max(0, dias);
    }

    public static DecisionCadencia Evaluar(int diasSinActividad, bool avisoRetomarYaEnviado, UmbralesCadencia? umbrales = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(diasSinActividad);
        var u = umbrales ?? new UmbralesCadencia();
        u.Validar();

        if (diasSinActividad >= u.DiasParaRetomar)
            return new DecisionCadencia(ModoResumen.Detenido, !avisoRetomarYaEnviado);
        if (diasSinActividad >= u.DiasParaSemanal)
            return new DecisionCadencia(ModoResumen.Semanal, false);
        if (diasSinActividad >= u.DiasParaRegreso)
            return new DecisionCadencia(ModoResumen.DiarioConRegreso, false);
        return new DecisionCadencia(ModoResumen.Diario, false);
    }
}
