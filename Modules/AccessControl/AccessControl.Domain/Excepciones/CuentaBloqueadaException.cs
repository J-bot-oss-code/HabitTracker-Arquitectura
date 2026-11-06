namespace AccessControl.Domain.Excepciones;

/// <summary>La cuenta está bloqueada 15 minutos por intentos fallidos consecutivos (RF-CA-19).</summary>
public sealed class CuentaBloqueadaException : ControlAccesoException
{
    public CuentaBloqueadaException() : base("cuenta_bloqueada", "Cuenta bloqueada temporalmente por intentos fallidos.") { }
}
