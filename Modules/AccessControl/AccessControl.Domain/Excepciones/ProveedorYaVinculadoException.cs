namespace AccessControl.Domain.Excepciones;

/// <summary>La cuenta ya está vinculada a otra identidad del proveedor, o esa identidad ya pertenece a otra cuenta.</summary>
public sealed class ProveedorYaVinculadoException : ControlAccesoException
{
    public ProveedorYaVinculadoException()
        : base("proveedor_ya_vinculado", "Esta cuenta o esa identidad de Google ya están vinculadas.") { }
}
