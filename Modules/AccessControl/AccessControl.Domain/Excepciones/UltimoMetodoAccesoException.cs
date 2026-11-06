namespace AccessControl.Domain.Excepciones;

/// <summary>Desvincular el proveedor dejaría la cuenta sin ninguna forma de iniciar sesión (no tiene contraseña).</summary>
public sealed class UltimoMetodoAccesoException : ControlAccesoException
{
    public UltimoMetodoAccesoException()
        : base("ultimo_metodo_acceso", "No puedes desvincular Google: es tu única forma de entrar. Crea primero una contraseña.") { }
}
