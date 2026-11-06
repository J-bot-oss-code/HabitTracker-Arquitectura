using AccessControl.Application.Interfaces;
using AccessControl.Domain.Excepciones;
using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessControl.Infrastructure.Security;

/// <summary>
/// Valida el ID token de Google con la librería oficial: firma contra las claves públicas de Google, emisor
/// (accounts.google.com), vencimiento y audiencia (Google:ClientId, así un token emitido para otra aplicación no sirve).
/// No usa secreto de cliente. Si falta Google:ClientId o no se puede consultar a Google, el inicio con Google responde 503
/// y el resto de la API sigue funcionando.
/// </summary>
public sealed class ValidadorIdTokenGoogle : IValidadorIdTokenGoogle
{
    public const string SeccionClientId = "Google:ClientId";

    private static readonly TimeSpan Tolerancia = TimeSpan.FromSeconds(30);

    private readonly IConfiguration _configuracion;
    private readonly ILogger<ValidadorIdTokenGoogle> _logger;
    private readonly Func<string, GoogleJsonWebSignature.ValidationSettings, Task<GoogleJsonWebSignature.Payload>> _validarFirma;

    public ValidadorIdTokenGoogle(IConfiguration configuracion, ILogger<ValidadorIdTokenGoogle> logger)
        : this(configuracion, logger, GoogleJsonWebSignature.ValidateAsync)
    {
    }

    /// <summary>Permite probar la traducción de resultados y errores sin llamar a Google.</summary>
    public ValidadorIdTokenGoogle(
        IConfiguration configuracion,
        ILogger<ValidadorIdTokenGoogle> logger,
        Func<string, GoogleJsonWebSignature.ValidationSettings, Task<GoogleJsonWebSignature.Payload>> validarFirma)
    {
        _configuracion = configuracion;
        _logger = logger;
        _validarFirma = validarFirma;
    }

    public async Task<IdentidadExterna> ValidarAsync(string idToken, CancellationToken cancelacion = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new TokenExternoInvalidoException();
        }

        var clientId = _configuracion[SeccionClientId]?.Trim();
        if (string.IsNullOrEmpty(clientId))
        {
            _logger.LogWarning("Inicio de sesión con Google solicitado, pero Google:ClientId no está configurado.");
            throw new ProveedorExternoNoDisponibleException();
        }

        var ajustes = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [clientId],
            ExpirationTimeClockTolerance = Tolerancia,
            IssuedAtClockTolerance = Tolerancia
        };

        try
        {
            var datos = await _validarFirma(idToken, ajustes);
            return new IdentidadExterna(datos.Subject, datos.Email, datos.EmailVerified, datos.Name);
        }
        catch (InvalidJwtException)
        {
            // Firma, emisor, audiencia o vencimiento incorrectos. No se registra el token ni el motivo exacto.
            _logger.LogInformation("ID token de Google rechazado.");
            throw new TokenExternoInvalidoException();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // No se pudieron descargar las claves de Google (sin red, caída o tiempo agotado): no es culpa del token.
            _logger.LogWarning("No se pudo consultar a Google para validar un ID token.");
            throw new ProveedorExternoNoDisponibleException(ex);
        }
    }
}
