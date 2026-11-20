namespace HabitTracker.Domain.Sugerencias;

public enum TipoSugerencia
{
    Queja,
    Mejora,
}

public enum EstadoSugerencia
{
    Nueva,
    Leida,
    Resuelta,
}

/// <summary>
/// Una queja o propuesta de mejora que una persona Estándar deja sobre la app, y que el Administrador revisa en su bandeja. El
/// texto es solo texto (se valida aquí y se escapa al mostrarse) y nunca pide datos de salud.
/// </summary>
public sealed class Sugerencia
{
    public const int LongitudMinima = 10;
    public const int LongitudMaxima = 2000;

    private Sugerencia() { } // EF Core

    public Sugerencia(Guid usuarioId, TipoSugerencia tipo, string texto, DateTime ahoraUtc)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("La persona es obligatoria.", nameof(usuarioId));
        if (!Enum.IsDefined(tipo))
            throw new ArgumentException("El tipo no es válido.", nameof(tipo));
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(ahoraUtc));

        var limpio = texto?.Trim() ?? "";
        if (limpio.Length is < LongitudMinima or > LongitudMaxima)
            throw new ArgumentException($"Cuéntanos con entre {LongitudMinima} y {LongitudMaxima} caracteres.", nameof(texto));

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Tipo = tipo;
        Texto = limpio;
        CreadaEnUtc = ahoraUtc;
        Estado = EstadoSugerencia.Nueva;
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public TipoSugerencia Tipo { get; private set; }
    public string Texto { get; private set; } = "";
    public DateTime CreadaEnUtc { get; private set; }
    public EstadoSugerencia Estado { get; private set; }
    public DateTime? EstadoCambiadoEnUtc { get; private set; }

    /// <summary>
    /// El único punto que cambia el estado (RD-04): Nueva → Leída → Resuelta, se puede resolver sin pasar por leída y una
    /// resuelta se puede reabrir como leída. Nada vuelve a «Nueva»: una vez vista, ya no es nueva. Repetir el mismo estado no
    /// cambia nada.
    /// </summary>
    public void CambiarEstado(EstadoSugerencia nuevo, DateTime ahoraUtc)
    {
        if (ahoraUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("La fecha debe ser UTC.", nameof(ahoraUtc));
        if (!Enum.IsDefined(nuevo) || (nuevo == EstadoSugerencia.Nueva && Estado != EstadoSugerencia.Nueva))
            throw new ArgumentException("Ese cambio de estado no es válido.", nameof(nuevo));
        if (nuevo == Estado)
            return;

        Estado = nuevo;
        EstadoCambiadoEnUtc = ahoraUtc;
    }
}
