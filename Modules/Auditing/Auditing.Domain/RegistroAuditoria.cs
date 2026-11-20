namespace Auditing.Domain;

/// <summary>
/// Un renglón del historial de auditoría (RF-AUD-01): quién actuó, qué hizo, sobre qué entidad, cuándo y cómo era el dato antes y
/// después. Es inmutable (RF-AUD-06): no tiene ningún método que lo cambie y todos sus datos son obligatorios, así que un registro
/// al que le falte alguno no se puede crear. Nunca debe recibir contraseñas, hashes ni códigos: quien lo escribe describe el valor
/// («Contraseña nueva»), no lo copia.
/// </summary>
public sealed class RegistroAuditoria
{
    public const int LongitudMaximaUsuario = 254;
    public const int LongitudMaximaAccion = 100;
    public const int LongitudMaximaEntidad = 100;
    public const int LongitudMaximaEntidadId = 100;
    public const int LongitudMaximaValor = 500;

    private RegistroAuditoria() { } // EF Core

    public RegistroAuditoria(
        Guid usuarioId, string usuario, string accion, string entidad, string entidadId,
        string valorAnterior, string valorNuevo, DateTime fechaUtc)
    {
        if (usuarioId == Guid.Empty)
            throw new ArgumentException("El usuario que actuó es obligatorio.", nameof(usuarioId));
        if (fechaUtc == default)
            throw new ArgumentException("La fecha y hora son obligatorias.", nameof(fechaUtc));

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        Usuario = Obligatorio(usuario, nameof(usuario), LongitudMaximaUsuario);
        Accion = Obligatorio(accion, nameof(accion), LongitudMaximaAccion);
        Entidad = Obligatorio(entidad, nameof(entidad), LongitudMaximaEntidad);
        EntidadId = Obligatorio(entidadId, nameof(entidadId), LongitudMaximaEntidadId);
        FechaUtc = DateTime.SpecifyKind(fechaUtc, DateTimeKind.Utc);
        // shortcut: un valor más largo que la columna se recorta (p. ej. un nombre de archivo enorme); subir el tope si hace falta.
        ValorAnterior = Recortado(Obligatorio(valorAnterior, nameof(valorAnterior), int.MaxValue));
        ValorNuevo = Recortado(Obligatorio(valorNuevo, nameof(valorNuevo), int.MaxValue));
    }

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    /// <summary>Correo de quien actuó, para leer el historial sin cruzar a la tabla de usuarios.</summary>
    public string Usuario { get; private set; } = string.Empty;
    public string Accion { get; private set; } = string.Empty;
    public string Entidad { get; private set; } = string.Empty;
    public string EntidadId { get; private set; } = string.Empty;
    public DateTime FechaUtc { get; private set; }
    public string ValorAnterior { get; private set; } = string.Empty;
    public string ValorNuevo { get; private set; } = string.Empty;

    private static string Obligatorio(string? valor, string campo, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new ArgumentException($"El dato «{campo}» es obligatorio.", campo);
        var limpio = valor.Trim();
        if (limpio.Length > maximo)
            throw new ArgumentException($"El dato «{campo}» supera los {maximo} caracteres.", campo);
        return limpio;
    }

    private static string Recortado(string valor) =>
        valor.Length <= LongitudMaximaValor ? valor : valor[..LongitudMaximaValor];
}
