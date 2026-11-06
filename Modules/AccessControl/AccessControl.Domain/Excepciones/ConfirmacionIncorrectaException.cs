namespace AccessControl.Domain.Excepciones;

/// <summary>
/// La confirmación de una operación delicada (la baja de cuenta) con la cuenta de Google no sirve: el token no es válido o
/// no es de esta cuenta. Se responde 400 y no 401: un 401 haría que el cliente cierre la sesión de quien solo se equivocó.
/// </summary>
public sealed class ConfirmacionIncorrectaException : ControlAccesoException
{
    public ConfirmacionIncorrectaException()
        : base("confirmacion_incorrecta", "No se pudo confirmar la operación con tu cuenta de Google.") { }
}
