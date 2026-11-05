using System.Security.Claims;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using WebApi.Extensions;
using WebApi.Paginas;

namespace WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;
    private readonly ITokenBlacklist _blacklist;
    private readonly IConsentimientoService _consentimientos;

    public AuthController(IUsuarioService usuarioService, ITokenBlacklist blacklist, IConsentimientoService consentimientos)
    {
        _usuarioService = usuarioService;
        _blacklist = blacklist;
        _consentimientos = consentimientos;
    }

    /// <summary>Registra un usuario con correo único (RF-CA-01, RF-CA-02).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Registro)]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegistroRequestDto request)
    {
        var resultado = await _usuarioService.RegistrarUsuarioAsync(request);
        return StatusCode(StatusCodes.Status201Created, resultado);
    }

    /// <summary>Activa una cuenta con el correo y el código de 6 dígitos recibido por correo.</summary>
    [HttpPost("activate")]
    [AllowAnonymous]
    [EnableRateLimiting("activacion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest request)
    {
        await _usuarioService.ActivarCuentaAsync(request.Correo, request.Codigo);
        return Ok(new { mensaje = "Cuenta activada correctamente." });
    }

    /// <summary>
    /// Página del enlace del correo de activación (RF-CA-15). Un GET nunca cambia el estado: la página tiene un botón
    /// que hace el POST de activación. Así un escáner de correos o una precarga de enlaces no activa la cuenta ni gasta
    /// los 5 intentos del código. No recibe parámetros: el navegador lee el correo y el código de la propia URL.
    /// </summary>
    [HttpGet("activate")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Activacion)]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ContentResult ActivateGet()
    {
        // La URL lleva el correo y el código: que no se guarde en cachés ni viaje en el Referer.
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; script-src 'nonce-{nonce}'; style-src 'nonce-{nonce}'; connect-src 'self'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
        return Content(PaginaActivacion.Html(nonce), "text/html");
    }

    /// <summary>
    /// Reenvía el enlace de activación. Retorna siempre 200 OK para no revelar
    /// si el correo existe (RF-CA-17).
    /// </summary>
    [HttpPost("resend-activation")]
    [AllowAnonymous]
    [EnableRateLimiting("activacion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResendActivation([FromBody] ResendActivationRequest request)
    {
        await _usuarioService.ReenviarEnlaceActivacionAsync(request.Correo);
        return Ok(new { mensaje = "Si el correo está registrado, recibirás un enlace de activación." });
    }

    /// <summary>Inicia sesión y devuelve el JWT (RF-CA-03).</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Login)]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var resultado = await _usuarioService.AutenticarUsuarioAsync(request);

        // Si cambiaron las versiones de los documentos legales (o la cuenta es anterior a esta función), el cliente debe
        // pedir que se vuelvan a aceptar. El inicio de sesión funciona igual: no se bloquea a nadie.
        var requiere = await _consentimientos.RequiereAceptarDocumentosAsync(resultado.Id);
        return Ok(resultado with { RequiereAceptarDocumentos = requiere });
    }

    /// <summary>Devuelve el usuario autenticado y su rol (RF-CA-07).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var requiere = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId)
            && await _consentimientos.RequiereAceptarDocumentosAsync(usuarioId);

        return Ok(new
        {
            id = User.FindFirstValue(ClaimTypes.NameIdentifier),
            nombre = User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name),
            correo = User.FindFirstValue(ClaimTypes.Email),
            rol = User.FindFirstValue(ClaimTypes.Role),
            requiereAceptarDocumentos = requiere
        });
    }

    /// <summary>Cierra la sesión invalidando el token actual (RF-CA-18).</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        var header = Request.Headers.Authorization.ToString();

        if (!string.IsNullOrWhiteSpace(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _blacklist.InvalidarToken(header["Bearer ".Length..].Trim());
        }

        return Ok(new { mensaje = "Sesión cerrada correctamente." });
    }

    /// <summary>
    /// Inicia la recuperación de contraseña. Retorna siempre 200 OK genérico
    /// para no revelar qué correos están registrados (RF-CA-09).
    /// </summary>
    [HttpPost("recuperar-password")]
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Recuperacion)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecuperarPassword([FromBody] RecuperarPasswordRequest request)
    {
        await _usuarioService.IniciarRecuperacionAsync(request.Correo);
        return Ok(new { mensaje = "Si el correo está registrado, recibirás un código de recuperación." });
    }

    /// <summary>Restablece la contraseña con un código válido (RF-CA-11).</summary>
    [HttpPost("restablecer-password")]
    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Recuperacion)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RestablecerPassword([FromBody] RestablecerPasswordRequest request)
    {
        await _usuarioService.RestablecerPasswordAsync(request.Correo, request.Codigo, request.NuevaPassword);
        return Ok(new { mensaje = "Contraseña restablecida correctamente." });
    }

    /// <summary>Cambia la contraseña del usuario autenticado (RF-CA-22).</summary>
    [HttpPost("cambiar-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CambiarPassword([FromBody] CambiarPasswordRequest request)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!Guid.TryParse(sub, out var usuarioId))
        {
            throw new UnauthorizedAccessException("Sesión inválida.");
        }

        await _usuarioService.CambiarPasswordAsync(usuarioId, request.Actual, request.Nueva);
        return Ok(new { mensaje = "Contraseña actualizada correctamente." });
    }
}

public record ActivateRequest(
    [param: Required, EmailAddress, StringLength(150)] string Correo,
    [param: Required, RegularExpression("^[0-9]{6}$", ErrorMessage = "El código debe tener 6 dígitos.")] string Codigo);

public record ResendActivationRequest(
    [param: Required, EmailAddress, StringLength(150)] string Correo);

public record RecuperarPasswordRequest(
    [param: Required, EmailAddress, StringLength(150)] string Correo);

public record RestablecerPasswordRequest(
    [param: Required, EmailAddress, StringLength(150)] string Correo,
    [param: Required, StringLength(64)] string Codigo,
    [param: Required, StringLength(1024)] string NuevaPassword);

public record CambiarPasswordRequest(
    [param: Required, StringLength(1024)] string Actual,
    [param: Required, StringLength(1024)] string Nueva);
