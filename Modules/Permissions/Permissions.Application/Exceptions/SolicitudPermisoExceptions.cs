namespace Permissions.Application.Exceptions;

/// <summary>La solicitud no existe. Se traduce a 404 con el código <c>solicitud_no_encontrada</c> (RD-08).</summary>
public sealed class SolicitudNoEncontradaException() : InvalidOperationException("La solicitud no existe.")
{
    public const string Codigo = "solicitud_no_encontrada";
}

/// <summary>
/// Ya hay una solicitud pendiente o aplicada del mismo permiso para esa persona. Se traduce a 409 con el código
/// <c>solicitud_duplicada</c>.
/// </summary>
public sealed class SolicitudDuplicadaException() : InvalidOperationException("Ya tienes una solicitud vigente de este permiso.")
{
    public const string Codigo = "solicitud_duplicada";
}
