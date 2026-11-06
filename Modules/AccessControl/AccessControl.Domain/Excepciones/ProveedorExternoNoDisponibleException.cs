namespace AccessControl.Domain.Excepciones;

/// <summary>
/// El inicio de sesión con un proveedor externo no se puede atender: falta su configuración (Google:ClientId) o no se
/// pudo consultar al proveedor. El middleware lo traduce a 503; el resto de la API sigue funcionando.
/// </summary>
public sealed class ProveedorExternoNoDisponibleException : Exception
{
    public const string CodigoProveedor = "proveedor_no_disponible";

    public string Codigo => CodigoProveedor;

    public ProveedorExternoNoDisponibleException() : base("El inicio de sesión con Google no está disponible.") { }

    public ProveedorExternoNoDisponibleException(Exception interna) : base("El inicio de sesión con Google no está disponible.", interna) { }
}
