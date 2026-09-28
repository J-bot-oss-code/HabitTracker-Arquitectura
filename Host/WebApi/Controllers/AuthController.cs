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

    public AuthController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    /// <summary>Registra un usuario con correo único (RF-CA-01, RF-CA-02).</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegistroRequestDto request)
    {
        try
        {
            var resultado = await _usuarioService.RegistrarUsuarioAsync(request);
            return StatusCode(StatusCodes.Status201Created, resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    /// <summary>Activa una cuenta con el token de activación.</summary>
    [HttpPost("activate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Activate([FromBody] ActivateRequest request)
    {
        try
        {
            await _usuarioService.ActivarCuentaAsync(request.Token);
            return Ok(new { mensaje = "Cuenta activada correctamente." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
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
        try
        {
            await _usuarioService.ReenviarEnlaceActivacionAsync(request.Correo);
            return Ok(new { mensaje = "Si el correo está registrado, recibirás un enlace de activación." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }
}

public record ActivateRequest(string Token);

public record ResendActivationRequest(string Correo);
