namespace AccessControl.Domain.Excepciones;

/// <summary>
/// Una cuenta nueva (creada con Google) necesita que la persona acepte las versiones vigentes de los documentos legales,
/// y la petición no los trae. El cliente los muestra y repite la petición con los consentimientos.
/// </summary>
public sealed class ConsentimientoRequeridoException : ControlAccesoException
{
    public ConsentimientoRequeridoException()
        : base("consentimiento_requerido", "Para crear la cuenta debes aceptar los Términos y la Política de privacidad vigentes.") { }
}
