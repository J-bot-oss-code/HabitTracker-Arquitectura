using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/admin/usuarios")]
[Authorize(Roles = "Administrador")]
public class AdminUsuariosController : ControllerBase
{
    private readonly IUsuarioService _usuarioService;

    public AdminUsuariosController(IUsuarioService usuarioService)
    {
        _usuarioService = usuarioService;
    }

    /// <summary>Lista todos los usuarios (RF-CA-21).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<UsuarioResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ObtenerTodos()
    {
        var usuarios = await _usuarioService.ObtenerTodosAsync();
        return Ok(usuarios);
    }

    /// <summary>Cambia el rol de un usuario (RF-CA-08).</summary>
    [HttpPatch("{id:guid}/rol")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CambiarRol(Guid id, [FromBody] CambiarRolRequest request)
    {
        await _usuarioService.cambiarRolAsync(id, request.NuevoRol, ObtenerAdminId());
        return Ok(new { mensaje = "Rol actualizado correctamente." });
    }

    /// <summary>Activa o desactiva un usuario (RF-CA-20).</summary>
    [HttpPatch("{id:guid}/estado")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoRequest request)
    {
        await _usuarioService.CambiarEstadoAsync(id, request.NuevoEstado, ObtenerAdminId());
        return Ok(new { mensaje = "Estado actualizado correctamente." });
    }

    private Guid ObtenerAdminId()
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(sub, out var adminId))
        {
            throw new UnauthorizedAccessException("Sesión inválida.");
        }

        return adminId;
    }
}

public record CambiarRolRequest(string NuevoRol);

public record CambiarEstadoRequest(bool NuevoEstado);
