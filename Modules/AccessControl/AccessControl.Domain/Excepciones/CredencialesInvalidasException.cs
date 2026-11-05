namespace AccessControl.Domain.Excepciones;

/// <summary>
/// Correo o contraseña incorrectos. No dice cuál de los dos falló (RF-CA-03). Es una UnauthorizedAccessException:
/// el middleware la traduce siempre a 401 en el inicio de sesión.
/// </summary>
public sealed class CredencialesInvalidasException : UnauthorizedAccessException
{
    public CredencialesInvalidasException() : base("Credenciales incorrectas") { }
}
