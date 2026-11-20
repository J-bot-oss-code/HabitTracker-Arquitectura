namespace HabitTracker.Domain.Notificaciones;

/// <summary>
/// Pasa una hora local ("8:00 en la zona de la persona") al próximo instante UTC en que ocurre (dominio puro). Es el único
/// punto del sistema que lo hace: el worker de recordatorios solo guarda y compara instantes UTC (RD-11).
/// </summary>
public static class CalculadoraProximoEnvio
{
    /// <summary>
    /// El primer instante UTC <b>posterior</b> a <paramref name="ahoraUtc"/> en que el reloj de <paramref name="zona"/> marca
    /// <paramref name="horaLocal"/>. Con el cambio de hora: una hora que no existe (salto hacia adelante) se corre una hora, y una
    /// que se repite (salto hacia atrás) cuenta su primera ocurrencia.
    /// </summary>
    public static DateTime ProximoEnvioUtc(TimeOnly horaLocal, TimeZoneInfo zona, DateTime ahoraUtc)
    {
        ArgumentNullException.ThrowIfNull(zona);
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(ahoraUtc));

        var diaLocal = TimeZoneInfo.ConvertTimeFromUtc(ahoraUtc, zona).Date;
        // Hoy o mañana bastan; el tercer día cubre una repetición de hora que ya pasó hoy.
        for (var dias = 0; dias <= 2; dias++)
        {
            var candidato = ALocalUtc(diaLocal.AddDays(dias) + horaLocal.ToTimeSpan(), zona);
            if (candidato > ahoraUtc)
                return candidato;
        }
        throw new InvalidOperationException("No se pudo calcular el próximo envío.");
    }

    private static DateTime ALocalUtc(DateTime local, TimeZoneInfo zona)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        // shortcut: el hueco del cambio de hora se asume de una hora (todas las zonas habituales); si hubiera uno de 30 min se
        // corrige a la hora siguiente, que sigue siendo un instante válido.
        if (zona.IsInvalidTime(local))
            local = local.AddHours(1);
        if (zona.IsAmbiguousTime(local))
            return DateTime.SpecifyKind(local - zona.GetAmbiguousTimeOffsets(local).Max(), DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeToUtc(local, zona);
    }
}
