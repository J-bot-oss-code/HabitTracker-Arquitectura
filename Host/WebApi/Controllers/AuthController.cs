using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;
    private readonly ITokenBlacklist _blacklist;

    public AuthController(IUsuarioService usuarioService, ITokenBlacklist blacklist)
    {
        _usuarioService = usuarioService;
        _blacklist = blacklist;
    }

    /// <summary>Registra un usuario con correo único (RF-CA-01, RF-CA-02).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegistroRequestDto request)
    {
        var resultado = await _usuarioService.RegistrarUsuarioAsync(request);
        return StatusCode(StatusCodes.Status201Created, resultado);
    }

    /// <summary>Activa una cuenta con el token de activación.</summary>
    [HttpPost("activate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest request)
    {
        await _usuarioService.ActivarCuentaAsync(request.Token);
        return Ok(new { mensaje = "Cuenta activada correctamente." });
    }

    /// <summary>
    /// Reenvía el enlace de activación. Retorna siempre 200 OK para no revelar
    /// si el correo existe (RF-CA-17).
    /// </summary>
    [HttpPost("resend-activation")]
    [AllowAnonymous]
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
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var resultado = await _usuarioService.AutenticarUsuarioAsync(request);
        return Ok(resultado);
    }

    /// <summary>Devuelve el usuario autenticado y su rol (RF-CA-07).</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            correo = User.FindFirstValue(JwtRegisteredClaimNames.Email),
            rol = User.FindFirstValue(ClaimTypes.Role)
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
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RecuperarPassword([FromBody] RecuperarPasswordRequest request)
    {
        await _usuarioService.IniciarRecuperacionAsync(request.Correo);
        return Ok(new { mensaje = "Si el correo está registrado, recibirás un código de recuperación." });
    }

    /// <summary>Restablece la contraseña con un código válido (RF-CA-11).</summary>
    [HttpPost("restablecer-password")]
    [AllowAnonymous]
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
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(sub, out var usuarioId))
        {
            throw new UnauthorizedAccessException("Sesión inválida.");
        }

        await _usuarioService.CambiarPasswordAsync(usuarioId, request.Actual, request.Nueva);
        return Ok(new { mensaje = "Contraseña actualizada correctamente." });
    }
}

public record ActivateRequest(string Token);

public record ResendActivationRequest(string Correo);

public record RecuperarPasswordRequest(string Correo);

public record RestablecerPasswordRequest(string Correo, string Codigo, string NuevaPassword);

public record CambiarPasswordRequest(string Actual, string Nueva);
