namespace AccessControl.Domain.Excepciones;

/// <summary>Código de activación o de recuperación incorrecto, usado, vencido o agotado (RF-CA-10, RF-CA-16).</summary>
public sealed class CodigoInvalidoException : ControlAccesoException
{
    public CodigoInvalidoException() : base("codigo_invalido", "El código es incorrecto, ya fue usado o ha expirado.") { }
}
