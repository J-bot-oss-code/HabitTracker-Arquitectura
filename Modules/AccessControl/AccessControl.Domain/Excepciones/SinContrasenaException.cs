namespace AccessControl.Domain.Excepciones;

/// <summary>La operación pide la contraseña actual y la cuenta no tiene (solo entra con Google). Puede crear una con la recuperación.</summary>
public sealed class SinContrasenaException : ControlAccesoException
{
    public SinContrasenaException()
        : base("sin_contrasena", "La cuenta no tiene contraseña. Puedes crear una con la recuperación de contraseña.") { }
}
