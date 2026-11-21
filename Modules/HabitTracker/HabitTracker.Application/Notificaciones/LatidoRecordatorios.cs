namespace HabitTracker.Application.Notificaciones;

/// <summary>
/// El «latido» del proceso de recordatorios: cuándo completó su última pasada. Lo escribe el worker y lo lee el chequeo de salud
/// (<c>/health</c>), así un proceso detenido o atascado se nota sin mirar los logs. No guarda ningún dato de personas.
/// </summary>
public interface ILatidoRecordatorios
{
    /// <summary>Cuándo arrancó el proceso (el punto de partida mientras no haya habido ninguna pasada).</summary>
    DateTime IniciadoEnUtc { get; }

    /// <summary>La última pasada que terminó sin error, o <c>null</c> si todavía no hubo ninguna.</summary>
    DateTime? UltimaPasadaCorrectaUtc { get; }

    void PasadaCorrecta(DateTime ahoraUtc);
}

public sealed class LatidoRecordatorios(TimeProvider? reloj = null) : ILatidoRecordatorios
{
    /// <summary>Pasadas seguidas que pueden faltar antes de dar aviso (una sola puede tardar o fallar de forma pasajera).</summary>
    public const int PasadasDeTolerancia = 3;

    /// <summary>Margen extra: el proceso espera un minuto antes de su primera pasada.</summary>
    public static readonly TimeSpan Margen = TimeSpan.FromMinutes(2);

    private long _ultimaTicks; // 0 = ninguna; Interlocked porque lo escribe el worker y lo lee el chequeo de salud

    public DateTime IniciadoEnUtc { get; } = (reloj ?? TimeProvider.System).GetUtcNow().UtcDateTime;

    public DateTime? UltimaPasadaCorrectaUtc =>
        Interlocked.Read(ref _ultimaTicks) is var t and > 0 ? new DateTime(t, DateTimeKind.Utc) : null;

    public void PasadaCorrecta(DateTime ahoraUtc)
    {
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(ahoraUtc));
        Interlocked.Exchange(ref _ultimaTicks, ahoraUtc.Ticks);
    }

    /// <summary>
    /// ¿Sigue vivo? Sí si desde la última pasada correcta (o desde el arranque, si aún no hubo ninguna) pasó menos de
    /// <see cref="PasadasDeTolerancia"/> intervalos más el <see cref="Margen"/>.
    /// </summary>
    public static bool EstaVivo(DateTime ahoraUtc, DateTime iniciadoEnUtc, DateTime? ultimaPasadaUtc, TimeSpan intervalo) =>
        ahoraUtc - (ultimaPasadaUtc ?? iniciadoEnUtc) <= intervalo * PasadasDeTolerancia + Margen;
}
