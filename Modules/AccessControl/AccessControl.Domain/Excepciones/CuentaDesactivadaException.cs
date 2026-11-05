namespace AccessControl.Domain.Excepciones;

/// <summary>Un administrador desactivó la cuenta (RF-CA-20).</summary>
public sealed class CuentaDesactivadaException : ControlAccesoException
{
    public CuentaDesactivadaException() : base("cuenta_desactivada", "La cuenta está desactivada. Contacte a un administrador.") { }
}
