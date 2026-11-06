namespace AccessControl.Domain.Excepciones;

/// <summary>Falta aceptar la versión vigente de los Términos de uso y de la Política de privacidad, o se envió una versión que no corresponde.</summary>
public sealed class ConsentimientoNoValidoException : ControlAccesoException
{
    public ConsentimientoNoValidoException()
        : base("consentimiento_no_valido", "Debes aceptar la versión vigente de los Términos de uso y de la Política de privacidad.") { }
}
