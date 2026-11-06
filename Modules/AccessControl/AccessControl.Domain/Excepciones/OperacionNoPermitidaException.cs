namespace AccessControl.Domain.Excepciones;

/// <summary>El ejecutor no tiene el rol que exige la operación (RF-CA-05, RF-CA-06).</summary>
public sealed class OperacionNoPermitidaException : ControlAccesoException
{
    public OperacionNoPermitidaException() : base("operacion_no_permitida", "No tiene permiso para realizar esta operación.") { }
}
