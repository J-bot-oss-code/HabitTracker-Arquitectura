namespace AccessControl.Domain.Excepciones;

/// <summary>La contraseña actual indicada al cambiarla no coincide (RF-CA-22).</summary>
public sealed class ClaveActualIncorrectaException : ControlAccesoException
{
    public ClaveActualIncorrectaException() : base("clave_actual_incorrecta", "La contraseña actual es incorrecta.") { }
}
