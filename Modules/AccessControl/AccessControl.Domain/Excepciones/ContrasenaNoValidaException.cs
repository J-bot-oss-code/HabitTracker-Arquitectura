namespace AccessControl.Domain.Excepciones;

/// <summary>La contraseña no cumple el formato mínimo: 12 a 1024 caracteres con al menos una letra y un número (RF-CA-14).</summary>
public sealed class ContrasenaNoValidaException : ControlAccesoException
{
    public ContrasenaNoValidaException()
        : base("contrasena_no_valida", "La contraseña debe tener al menos 12 caracteres e incluir letras y números.") { }
}
