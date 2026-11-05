namespace AccessControl.Domain.Excepciones;

/// <summary>La operación dejaría al sistema sin ningún administrador activo (RF-CA-08, RF-CA-20).</summary>
public sealed class UltimoAdministradorException : ControlAccesoException
{
    public UltimoAdministradorException() : base("ultimo_administrador", "Debe existir al menos un administrador activo.") { }
}
