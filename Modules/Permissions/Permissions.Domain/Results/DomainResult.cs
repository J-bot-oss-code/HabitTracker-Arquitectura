namespace Permissions.Domain.Results;

public enum DomainError
{
    Ninguno,
    AprobadorRequerido,
    AutoResolucionProhibida,
    EstadoTransicionInvalido,
    MotivoRechazoRequerido,
    FechaResolucionDebeSerUtc
}

public sealed record DomainResult
{
    public bool IsSuccess { get; }
    public DomainError Error { get; }

    private DomainResult(bool isSuccess, DomainError error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public string? ErrorMessage => Error switch
    {
        DomainError.AprobadorRequerido => "El aprobador es obligatorio.",
        DomainError.AutoResolucionProhibida => "El solicitante no puede resolver su propia solicitud.",
        DomainError.EstadoTransicionInvalido => "La solicitud no permite esta transición de estado.",
        DomainError.MotivoRechazoRequerido => "El motivo del rechazo es obligatorio.",
        DomainError.FechaResolucionDebeSerUtc => "La fecha de resolución debe estar expresada en UTC.",
        _ => null
    };

    public static DomainResult Success() => new(true, DomainError.Ninguno);

    public static DomainResult Failure(DomainError error)
    {
        if (error == DomainError.Ninguno)
        {
            throw new ArgumentException("El resultado fallido requiere un código de error.", nameof(error));
        }

        return new(false, error);
    }
}
