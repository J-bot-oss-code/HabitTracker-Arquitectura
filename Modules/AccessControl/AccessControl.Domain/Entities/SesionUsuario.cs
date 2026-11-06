namespace AccessControl.Domain.Entities;

/// <summary>
/// Una sesión de un usuario en un dispositivo, identificada por su refresh token. Del token solo se guarda el hash
/// (SHA-256): si alguien leyera la base de datos no podría usarlo. Cada renovación REEMPLAZA la sesión por otra de la
/// misma familia; si llega un token que ya fue reemplazado o revocado, es señal de robo y se revoca toda la familia.
/// </summary>
public class SesionUsuario
{
    public const int LongitudMaximaDispositivo = 200;
    public const int LongitudMaximaIp = 45;
    public const int LongitudHash = 64; // SHA-256 en hexadecimal
    public const string DispositivoDesconocido = "Dispositivo desconocido";

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }

    /// <summary>Todas las sesiones que descienden de un mismo inicio de sesión comparten la familia.</summary>
    public Guid FamiliaId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    /// <summary>Cuándo se inició la sesión (el inicio de sesión original: las renovaciones lo conservan).</summary>
    public DateTime CreadaEn { get; private set; }

    public DateTime ExpiraEn { get; private set; }
    public DateTime UltimoUsoEn { get; private set; }
    public DateTime? RevocadaEn { get; private set; }

    /// <summary>La sesión que la sustituyó al renovarse. Es nula si se revocó por cierre de sesión u otro motivo.</summary>
    public Guid? ReemplazadaPorId { get; private set; }

    public string Dispositivo { get; private set; } = null!;
    public string? IpCreacion { get; private set; }

    private SesionUsuario() { } // Constructor privado para EF Core

    /// <summary>Primera sesión de una familia nueva (inicio de sesión).</summary>
    public SesionUsuario(Guid usuarioId, string tokenHash, DateTime ahora, TimeSpan duracion, string? dispositivo, string? ip)
        : this(Guid.NewGuid(), usuarioId, Guid.NewGuid(), tokenHash, ahora, ahora, duracion, dispositivo, ip)
    {
    }

    private SesionUsuario(
        Guid id, Guid usuarioId, Guid familiaId, string tokenHash, DateTime creadaEn, DateTime ahora,
        TimeSpan duracion, string? dispositivo, string? ip)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("El usuario no es válido.", nameof(usuarioId));
        }

        if (string.IsNullOrWhiteSpace(tokenHash) || tokenHash.Length != LongitudHash)
        {
            throw new ArgumentException("El hash del token de sesión no es válido.", nameof(tokenHash));
        }

        if (duracion <= TimeSpan.Zero)
        {
            throw new ArgumentException("La duración de la sesión debe ser mayor a cero.", nameof(duracion));
        }

        Id = id;
        UsuarioId = usuarioId;
        FamiliaId = familiaId;
        TokenHash = tokenHash;
        CreadaEn = creadaEn;
        UltimoUsoEn = ahora;
        ExpiraEn = ahora.Add(duracion);
        Dispositivo = NormalizarDispositivo(dispositivo);
        IpCreacion = NormalizarIp(ip);
    }

    /// <summary>La sesión que sustituye a esta al renovar: misma familia, mismo dispositivo y nueva vigencia.</summary>
    public SesionUsuario SiguienteEnLaFamilia(string nuevoTokenHash, DateTime ahora, TimeSpan duracion) =>
        new(Guid.NewGuid(), UsuarioId, FamiliaId, nuevoTokenHash, CreadaEn, ahora, duracion, Dispositivo, IpCreacion);

    public bool EstaActiva(DateTime ahora) => RevocadaEn is null && ExpiraEn > ahora;

    public void Revocar(DateTime ahora) => RevocadaEn ??= ahora;

    /// <summary>Quita caracteres de control (el texto se muestra en la lista de sesiones) y limita la longitud.</summary>
    private static string NormalizarDispositivo(string? dispositivo)
    {
        if (string.IsNullOrWhiteSpace(dispositivo))
        {
            return DispositivoDesconocido;
        }

        var limpio = new string(dispositivo.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (limpio.Length == 0)
        {
            return DispositivoDesconocido;
        }

        return limpio.Length > LongitudMaximaDispositivo ? limpio[..LongitudMaximaDispositivo] : limpio;
    }

    private static string? NormalizarIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return null;
        }

        var limpia = ip.Trim();
        return limpia.Length > LongitudMaximaIp ? limpia[..LongitudMaximaIp] : limpia;
    }
}
