namespace AccessControl.Application.Interfaces;

/// <summary>Versiones vigentes de los documentos legales. Cuando cambian, quien aceptó una versión anterior debe volver a aceptar.</summary>
public interface IVersionesLegales
{
    string Terminos { get; }
    string Privacidad { get; }
}
