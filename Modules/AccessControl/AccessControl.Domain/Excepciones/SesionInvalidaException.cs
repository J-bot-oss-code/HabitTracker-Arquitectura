namespace AccessControl.Domain.Excepciones;

/// <summary>
/// El refresh token falta, no existe, venció, fue revocado o ya se había usado. El mensaje no dice cuál de los casos
/// es. Es una UnauthorizedAccessException: el middleware la traduce siempre a 401 con el código "sesion_invalida".
/// </summary>
public sealed class SesionInvalidaException : UnauthorizedAccessException
{
    public const string CodigoSesionInvalida = "sesion_invalida";

    public string Codigo => CodigoSesionInvalida;

    public SesionInvalidaException() : base("La sesión ya no es válida. Inicie sesión nuevamente.") { }
}
