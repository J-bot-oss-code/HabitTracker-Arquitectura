namespace AccessControl.Domain.Excepciones;

/// <summary>La contraseña aparece en filtraciones de datos conocidas: no se acepta aunque cumpla el formato (RF-CA-14).</summary>
public sealed class ContrasenaFiltradaException : ControlAccesoException
{
    public ContrasenaFiltradaException()
        : base("contrasena_filtrada", "Esa contraseña apareció en filtraciones de datos conocidas. Elige otra distinta.") { }
}
