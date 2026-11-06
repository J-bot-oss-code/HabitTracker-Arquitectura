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
    private readonly IServicioLoginExterno _loginExterno;

    public AuthController(
        IUsuarioService usuarioService,
        ITokenBlacklist blacklist,
        IConsentimientoService consentimientos,
        IGestorSesiones sesiones,
        OpcionesSesion opcionesSesion,
        IServicioLoginExterno loginExterno)
    {
        _loginExterno = loginExterno;
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
        return Ok(await CompletarInicioAsync(resultado));
    }

    /// <summary>
    /// Entra con Google. El navegador obtiene un ID token con Google Identity Services y lo envía aquí; la API lo valida
    /// (firma, emisor, vencimiento y que fue emitido para esta aplicación) y exige que Google haya verificado el correo.
    /// Si la identidad ya está vinculada inicia sesión (200); si el correo es nuevo crea la cuenta (201, exige
    /// <c>consentimientos</c>, o responde 400 <c>consentimiento_requerido</c>); si el correo ya tiene una cuenta con
    /// contraseña responde 409 <c>vinculacion_requerida</c> y hay que usar /google/vincular. Abre la sesión igual que el login.
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Google)]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Google([FromBody] GoogleLoginRequest request)
    {
        var resultado = await _loginExterno.IniciarConGoogleAsync(request.IdToken, request.Consentimientos);
        var respuesta = await CompletarInicioAsync(resultado.Usuario);

        return resultado.CuentaNueva ? StatusCode(StatusCodes.Status201Created, respuesta) : Ok(respuesta);
    }

    /// <summary>
    /// Vincula Google a la cuenta con contraseña de ese mismo correo. Pide la contraseña: sin ella, quien controle el
    /// correo en Google podría apoderarse de una cuenta ajena. Cuenta para el bloqueo por intentos fallidos (RF-CA-19).
    /// </summary>
    [HttpPost("google/vincular")]
    [AllowAnonymous]
    [EnableRateLimiting(LimitacionDePeticionesExtensions.Google)]
    [ProducesResponseType(typeof(UsuarioResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> VincularGoogle([FromBody] GoogleVincularRequest request)
    {
        var resultado = await _loginExterno.VincularGoogleAsync(request.IdToken, request.Password);
        return Ok(await CompletarInicioAsync(resultado.Usuario));
    }

    /// <summary>Formas de entrar de la cuenta autenticada: si tiene contraseña y qué proveedores tiene vinculados.</summary>
    [HttpGet("vinculos")]
    [Authorize]
    [ProducesResponseType(typeof(VinculosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ListarVinculos()
    {
        return Ok(await _loginExterno.ListarVinculosAsync(UsuarioIdActual()));
    }

    /// <summary>Desvincula Google. Solo se permite si la cuenta tiene contraseña (si no, quedaría sin forma de entrar).</summary>
    [HttpDelete("vinculos/{proveedor}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Desvincular(string proveedor)
    {
        var desvinculado = proveedor == AccessControl.Domain.Entities.LoginExterno.ProveedorGoogle
            && await _loginExterno.DesvincularAsync(UsuarioIdActual(), proveedor);

        if (!desvinculado)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "No encontrado.",
                Detail = "La cuenta no tiene ese proveedor vinculado.",
                Instance = HttpContext.TraceIdentifier
            });
        }

        return NoContent();
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

    /// <summary>
    /// Abre la sesión (cookie del refresh token) y completa la respuesta del inicio de sesión. Si cambiaron las versiones de
    /// los documentos legales (o la cuenta es anterior a esa función), el cliente debe pedir que se vuelvan a aceptar; el
    /// inicio de sesión funciona igual: no se bloquea a nadie.
    /// </summary>
    private async Task<UsuarioResponseDto> CompletarInicioAsync(UsuarioResponseDto usuario)
    {
        await AbrirSesionAsync(usuario.Id);
        var requiere = await _consentimientos.RequiereAceptarDocumentosAsync(usuario.Id);
        return usuario with { RequiereAceptarDocumentos = requiere };
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

public record GoogleLoginRequest(
    [param: Required, StringLength(4096)] string IdToken,
    ConsentimientosRegistroDto? Consentimientos = null);

public record GoogleVincularRequest(
    [param: Required, StringLength(4096)] string IdToken,
    [param: Required, StringLength(1024)] string Password);

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
