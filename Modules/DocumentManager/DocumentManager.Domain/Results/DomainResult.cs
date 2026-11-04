namespace DocumentManager.Domain.Results;

public enum DomainError
{
    Ninguno,
    DocumentoYaEliminado,
    FechaEliminacionDebeSerUtc
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
        DomainError.DocumentoYaEliminado => "El documento ya fue eliminado.",
        DomainError.FechaEliminacionDebeSerUtc => "La fecha de eliminación debe estar expresada en UTC.",
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
