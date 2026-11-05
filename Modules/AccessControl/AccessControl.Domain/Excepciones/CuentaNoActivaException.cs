namespace AccessControl.Domain.Excepciones;

/// <summary>La cuenta existe pero todavía no se activó con el código enviado por correo (RF-CA-15).</summary>
public sealed class CuentaNoActivaException : ControlAccesoException
{
    public CuentaNoActivaException() : base("cuenta_no_activa", "Debe activar su cuenta antes de iniciar sesión.") { }
}
