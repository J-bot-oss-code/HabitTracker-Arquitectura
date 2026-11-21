namespace HabitTracker.Domain.Notificaciones;

/// <summary>Qué recordatorio le toca hoy a un plan.</summary>
public enum TipoRecordatorio
{
    /// <summary>Lo de hoy, con su botón «Ya lo hice».</summary>
    Diario,

    /// <summary>Igual, con el mensaje de regreso (P3: volver es parte del plan). También el semanal.</summary>
    Regreso,

    /// <summary>El único «¿seguimos?»: después de él no se envía nada hasta que la persona vuelva a registrar algo.</summary>
    AvisoRetomar,
}

/// <summary>
/// Decide si a un plan le toca su recordatorio por correo en esta pasada (dominio puro). Combina la hora local elegida, una
/// ventana para no mandar a destiempo (si el proceso estuvo parado), un envío por día como máximo y la
/// <see cref="CalculadoraCadencia"/>, que <b>solo reduce</b>.
/// </summary>
public static class DecisorRecordatorio
{
    /// <summary>Pasada la hora elegida más esta ventana, el recordatorio de ese día ya no se envía (llegaría a destiempo).</summary>
    public static readonly TimeSpan Ventana = TimeSpan.FromHours(3);

    /// <param name="hora">La hora local elegida para el plan.</param>
    /// <param name="ultimoEnvioLocal">El último día local en que se envió el recordatorio de este plan.</param>
    /// <param name="diasSinActividad">Días locales completos desde el último registro (<see cref="CalculadoraCadencia.DiasSinActividad"/>).</param>
    /// <returns>El tipo de recordatorio, o <c>null</c> si en esta pasada no toca ninguno.</returns>
    public static TipoRecordatorio? Decidir(
        TimeOnly hora, TimeZoneInfo zona, DateTime ahoraUtc, DateOnly? ultimoEnvioLocal, int diasSinActividad,
        UmbralesCadencia? umbrales = null)
    {
        ArgumentNullException.ThrowIfNull(zona);
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(ahoraUtc));
        ArgumentOutOfRangeException.ThrowIfNegative(diasSinActividad);

        var ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zona);
        var hoy = DateOnly.FromDateTime(ahoraLocal);
        if (ultimoEnvioLocal >= hoy)
            return null;

        // Dentro de [hora, hora + ventana] del día local. Una hora tardía cuya ventana cruza la medianoche se corta ahí: un
        // recordatorio de las 23:00 no se manda a la 1:00 del día siguiente como si fuera de ese día.
        var desde = ahoraLocal.Date + hora.ToTimeSpan();
        if (ahoraLocal < desde || ahoraLocal > desde + Ventana)
            return null;

        var u = umbrales ?? new UmbralesCadencia();
        // El «¿seguimos?» se envía el día en que se llega al umbral, una sola vez: después ya pasó y la cadencia queda detenida.
        var decision = CalculadoraCadencia.Evaluar(diasSinActividad, avisoRetomarYaEnviado: diasSinActividad > u.DiasParaRetomar, u);

        return decision switch
        {
            { EnviarAvisoRetomar: true } => TipoRecordatorio.AvisoRetomar,
            { Modo: ModoResumen.Detenido } => null,
            // Semanal: el día del umbral y luego cada 7 días.
            { Modo: ModoResumen.Semanal } => (diasSinActividad - u.DiasParaSemanal) % 7 == 0 ? TipoRecordatorio.Regreso : null,
            { Modo: ModoResumen.DiarioConRegreso } => TipoRecordatorio.Regreso,
            _ => TipoRecordatorio.Diario,
        };
    }
}
