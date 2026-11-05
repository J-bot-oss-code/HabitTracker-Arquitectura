using System.Security.Claims;
using System.Security.Cryptography;
using System.ComponentModel.DataAnnotations;
using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Application.Service;
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
    private readonly IGestorSesiones _sesiones;
    private readonly OpcionesSesion _opcionesSesion;

    public AuthController(
        IUsuarioService usuarioService,
        ITokenBlacklist blacklist,
        IConsentimientoService consentimientos,
        IGestorSesiones sesiones,
        OpcionesSesion opcionesSesion)
    {
        _usuarioService = usuarioService;
        _blacklist = blacklist;
        _consentimientos = consentimientos;
        _sesiones = sesiones;
        _opcionesSesion = opcionesSesion;
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

    /// <summary>
    /// Inicia sesión y devuelve el JWT de acceso (RF-CA-03), que dura pocos minutos. Además abre la sesión y fija la cookie
    /// HttpOnly con el refresh token, con el que /refresh renueva el acceso sin volver a pedir la contraseña.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Login)]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        var resultado = await _usuarioService.AutenticarUsuarioAsync(request);
        await AbrirSesionAsync(resultado.Id);

        // Si cambiaron las versiones de los documentos legales (o la cuenta es anterior a esta función), el cliente debe
        // pedir que se vuelvan a aceptar. El inicio de sesión funciona igual: no se bloquea a nadie.
        var requiere = await _consentimientos.RequiereAceptarDocumentosAsync(resultado.Id);
        return Ok(resultado with { RequiereAceptarDocumentos = requiere });
    }

    /// <summary>
    /// Renueva el token de acceso con el refresh token de la cookie (rota: el anterior queda revocado y la cookie se
    /// reemplaza). Como se autentica solo con la cookie, exige la cabecera X-HabitTracker-Csrf: un formulario de otro
    /// sitio no puede enviarla, y CORS impide que un script de otro origen la envíe.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Refresh)]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Refresh()
    {
        if (!Request.Headers.TryGetValue(OpcionesSesion.CabeceraCsrf, out var csrf) || csrf != "1")
        {
            var problema = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Solicitud no permitida.",
                Detail = "Falta la cabecera de protección de la solicitud.",
                Instance = HttpContext.TraceIdentifier
            };
            problema.Extensions["codigo"] = "csrf_requerido";
            return StatusCode(StatusCodes.Status403Forbidden, problema);
        }

        SesionRenovada renovada;
        try
        {
            renovada = await _sesiones.RenovarAsync(CookieDeSesion.Leer(Request));
        }
        catch (AccessControl.Domain.Excepciones.SesionInvalidaException)
        {
            // La cookie ya no sirve: se borra para que el navegador no la siga enviando.
            CookieDeSesion.Borrar(Response, _opcionesSesion);
            throw;
        }

        CookieDeSesion.Establecer(Response, _opcionesSesion, renovada.RefreshToken, renovada.ExpiraEn);

        var requiere = await _consentimientos.RequiereAceptarDocumentosAsync(renovada.Usuario.Id);
        return Ok(renovada.Usuario with { RequiereAceptarDocumentos = requiere });
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

    /// <summary>
    /// Cierra la sesión: invalida el token de acceso actual (RF-CA-18) y revoca el refresh token de la cookie. Se autoriza
    /// con el Bearer, no con la cookie, así que no necesita la cabecera anti-CSRF.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var header = Request.Headers.Authorization.ToString();

        if (!string.IsNullOrWhiteSpace(header) && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _blacklist.InvalidarToken(header["Bearer ".Length..].Trim());
        }

        await _sesiones.CerrarAsync(UsuarioIdActual(), CookieDeSesion.Leer(Request));
        CookieDeSesion.Borrar(Response, _opcionesSesion);

        return Ok(new { mensaje = "Sesión cerrada correctamente." });
    }

    /// <summary>Cierra la sesión en todos los dispositivos: revoca todos los refresh tokens y los tokens de acceso emitidos.</summary>
    [HttpPost("logout-todas")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LogoutTodas()
    {
        await _sesiones.RevocarTodasAsync(UsuarioIdActual(), "cierre-en-todos-los-dispositivos");
        CookieDeSesion.Borrar(Response, _opcionesSesion);

        return Ok(new { mensaje = "Se cerró la sesión en todos los dispositivos." });
    }

    /// <summary>Lista las sesiones abiertas del usuario autenticado. La del dispositivo que consulta viene marcada como actual.</summary>
    [HttpGet("sesiones")]
    [Authorize]
    [ProducesResponseType(typeof(IReadOnlyList<SesionActivaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListarSesiones()
    {
        return Ok(await _sesiones.ListarAsync(UsuarioIdActual(), CookieDeSesion.Leer(Request)));
    }

    /// <summary>Cierra una de las sesiones del usuario autenticado (nunca la de otra persona: responde 404).</summary>
    [HttpDelete("sesiones/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevocarSesion(Guid id)
    {
        var revocada = await _sesiones.RevocarAsync(UsuarioIdActual(), id);
        if (!revocada)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "No encontrado.",
                Detail = "La sesión no existe.",
                Instance = HttpContext.TraceIdentifier
            });
        }

        return NoContent();
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

    /// <summary>La identidad sale siempre del token, nunca de la petición (RD-06).</summary>
    private Guid UsuarioIdActual()
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var usuarioId))
        {
            throw new UnauthorizedAccessException("Sesión inválida.");
        }

        return usuarioId;
    }

    private async Task AbrirSesionAsync(Guid usuarioId)
    {
        var sesion = await _sesiones.IniciarAsync(
            usuarioId, Request.Headers.UserAgent.ToString(), HttpContext.Connection.RemoteIpAddress?.ToString());
        CookieDeSesion.Establecer(Response, _opcionesSesion, sesion.RefreshToken, sesion.ExpiraEn);
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
