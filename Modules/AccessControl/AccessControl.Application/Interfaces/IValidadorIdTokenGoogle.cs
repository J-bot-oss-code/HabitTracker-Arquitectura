namespace AccessControl.Application.Interfaces;

/// <summary>Lo que Google afirma de la persona que inició sesión, ya verificado por el validador.</summary>
public sealed record IdentidadExterna(string Subject, string Correo, bool CorreoVerificado, string? Nombre);

/// <summary>
/// Valida el ID token que el navegador obtuvo de Google Identity Services: firma, emisor, vencimiento y que fue emitido
/// para NUESTRA aplicación (Google:ClientId). No usa secreto de cliente ni redirecciones.
/// </summary>
public interface IValidadorIdTokenGoogle
{
    /// <exception cref="Domain.Excepciones.TokenExternoInvalidoException">El token no es válido.</exception>
    /// <exception cref="Domain.Excepciones.ProveedorExternoNoDisponibleException">Falta Google:ClientId o no se pudo consultar a Google.</exception>
    Task<IdentidadExterna> ValidarAsync(string idToken, CancellationToken cancelacion = default);
}
