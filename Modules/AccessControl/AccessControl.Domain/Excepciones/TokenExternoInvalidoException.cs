namespace AccessControl.Domain.Excepciones;

/// <summary>
/// El token del proveedor externo no sirve: firma o emisor incorrectos, vencido, de otra aplicación o con el correo sin
/// verificar. No dice cuál de los motivos es. Es una UnauthorizedAccessException: el middleware la traduce a 401.
/// </summary>
public sealed class TokenExternoInvalidoException : UnauthorizedAccessException
{
    public const string CodigoTokenExterno = "token_externo_invalido";

    public string Codigo => CodigoTokenExterno;

    public TokenExternoInvalidoException() : base("No se pudo verificar la cuenta de Google.") { }
}
