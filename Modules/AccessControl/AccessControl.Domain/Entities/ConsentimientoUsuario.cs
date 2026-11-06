namespace AccessControl.Domain.Entities;

/// <summary>
/// Aceptación de los documentos legales y de los consentimientos opcionales de un usuario. Es un historial de SOLO
/// INSERCIÓN: cada vez que algo cambia se agrega una fila nueva y la vigente es la última. Así queda constancia de qué
/// versión de los Términos y de la Política de privacidad aceptó cada persona y cuándo (Ley 172-13 de la República
/// Dominicana y GDPR: el consentimiento debe poder demostrarse).
/// </summary>
public class ConsentimientoUsuario
{
    public const string OrigenRegistro = "registro";
    public const string OrigenGoogle = "google";
    public const string OrigenActualizacion = "actualizacion";

    private static readonly string[] OrigenesValidos = [OrigenRegistro, OrigenGoogle, OrigenActualizacion];

    public Guid Id { get; private set; }
    public Guid UsuarioId { get; private set; }
    public string TerminosVersion { get; private set; } = null!;
    public string PrivacidadVersion { get; private set; } = null!;
    public bool DatosSalud { get; private set; }
    public bool Marketing { get; private set; }
    public bool CompartirCumplimiento { get; private set; }
    public bool EstadisticasAnonimas { get; private set; }

    /// <summary>Instante UTC en que la persona aceptó ESTAS versiones. Se conserva cuando solo cambia un interruptor.</summary>
    public DateTime AceptadoEn { get; private set; }

    /// <summary>Instante UTC en que se escribió esta fila (siempre lo fija el servidor).</summary>
    public DateTime ActualizadoEn { get; private set; }

    /// <summary>De dónde vino: registro con correo, inicio con Google o una actualización posterior.</summary>
    public string Origen { get; private set; } = null!;

    private ConsentimientoUsuario() { } // Constructor privado para EF Core

    public ConsentimientoUsuario(
        Guid usuarioId,
        string terminosVersion,
        string privacidadVersion,
        bool datosSalud,
        bool marketing,
        bool compartirCumplimiento,
        bool estadisticasAnonimas,
        DateTime aceptadoEn,
        DateTime ahora,
        string origen)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("El usuario no es válido.", nameof(usuarioId));
        }

        if (string.IsNullOrWhiteSpace(terminosVersion) || terminosVersion.Length > 20)
        {
            throw new ArgumentException("La versión de los Términos no es válida.", nameof(terminosVersion));
        }

        if (string.IsNullOrWhiteSpace(privacidadVersion) || privacidadVersion.Length > 20)
        {
            throw new ArgumentException("La versión de la Política de privacidad no es válida.", nameof(privacidadVersion));
        }

        if (!OrigenesValidos.Contains(origen))
        {
            throw new ArgumentException("El origen del consentimiento no es válido.", nameof(origen));
        }

        if (aceptadoEn > ahora)
        {
            throw new ArgumentException("La aceptación no puede ser posterior al momento actual.", nameof(aceptadoEn));
        }

        Id = Guid.NewGuid();
        UsuarioId = usuarioId;
        TerminosVersion = terminosVersion;
        PrivacidadVersion = privacidadVersion;
        DatosSalud = datosSalud;
        Marketing = marketing;
        CompartirCumplimiento = compartirCumplimiento;
        EstadisticasAnonimas = estadisticasAnonimas;
        AceptadoEn = aceptadoEn;
        ActualizadoEn = ahora;
        Origen = origen;
    }

    /// <summary>Consentimiento con el que nace una cuenta: solo los documentos legales y, opcional, el marketing.</summary>
    public static ConsentimientoUsuario DelRegistro(
        Guid usuarioId, string terminosVersion, string privacidadVersion, bool marketing, DateTime ahora, string origen = OrigenRegistro) =>
        new(usuarioId, terminosVersion, privacidadVersion,
            datosSalud: false, marketing, compartirCumplimiento: false, estadisticasAnonimas: false,
            aceptadoEn: ahora, ahora, origen);

    /// <summary>¿Estas son las versiones vigentes de ambos documentos?</summary>
    public bool EsDeLasVersiones(string terminosVigente, string privacidadVigente) =>
        TerminosVersion == terminosVigente && PrivacidadVersion == privacidadVigente;
}
