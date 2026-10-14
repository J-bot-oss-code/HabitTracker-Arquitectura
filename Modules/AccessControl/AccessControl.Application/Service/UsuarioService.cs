using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Interfaces;
using AccessControl.Domain.Entities.ValueObjects;
using Microsoft.Extensions.Logging;
using AccessControl.Domain.Entities;
using System.Diagnostics;

namespace AccessControl.Application.Service;

public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailQueue _emailQueue;
    private readonly IAuditoriaService _auditoria;
    private readonly IJwtProvider _jwtProvider;
    private readonly ITokenBlacklist _blacklist;
    private readonly ILogger<UsuarioService> _logger;
    public UsuarioService(
        IUsuarioRepository repository, 
        IPasswordHasher passwordHasher, 
        IEmailQueue emailQueue,
        IAuditoriaService auditoria,
        IJwtProvider jwtProvider,
        ITokenBlacklist blacklist,
        ILogger<UsuarioService> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _emailQueue = emailQueue;
        _auditoria = auditoria;
        _jwtProvider = jwtProvider;
        _blacklist = blacklist;
        _logger = logger;
    }

   

    public async Task<UsuarioResponseDto> AutenticarUsuarioAsync(LoginRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = new Email(request.Email);
        if (string.IsNullOrWhiteSpace(request.password) || request.password.Length > 1024)
        {
            throw new ArgumentException("Las credenciales no son válidas.", nameof(request));
        }

        _logger.LogInformation("Intento de login para: {Email}", request.Email);

        var usuario = await _repository.GetByCorreoAsync(email);

        // 1. RF-CA-03: sin revelar qué dato falló.
        if (usuario == null)
        {
            throw new UnauthorizedAccessException("Credenciales incorrectas");
        }

        // 2. Se verifica la clave ANTES de revelar si la cuenta está inactiva:
        // una clave incorrecta en cuenta activa o inactiva responde 401 genérico.
        // Solo si ya está bloqueada (el atacante provocó el bloqueo) se informa el bloqueo.
        if (!_passwordHasher.verificar(usuario.PasswordHash, request.password))
        {
            usuario.RegistrarIntentoFallido();
            await _repository.UpdateAsync(usuario);

            if (usuario.EstaBloqueado())
            {
                throw new InvalidOperationException("Cuenta bloqueada temporalmente por intentos fallidos.");
            }

            throw new UnauthorizedAccessException("Credenciales incorrectas");
        }

        // 3. RF-CA-19: cuenta bloqueada por intentos fallidos (solo con clave correcta).
        if (usuario.EstaBloqueado())
        {
            throw new InvalidOperationException("Cuenta bloqueada temporalmente por intentos fallidos.");
        }

        // 4. RF-CA-15: la cuenta debe estar activada (solo con clave correcta).
        if (!usuario.Activo)
        {
            throw new InvalidOperationException("Debe activar su cuenta antes de iniciar sesión.");
        }

        // 5. Éxito: se restablecen intentos, se persiste y se emite el JWT.
        usuario.RestablecerIntentos();
        await _repository.UpdateAsync(usuario);

        var token = _jwtProvider.Generar(usuario);

        return new UsuarioResponseDto(
            usuario.Id,
            usuario.NombreCompleto,
            usuario.Correo.Valor,
            usuario.Rol.ToString(),
            usuario.Activo,
            token
        );
    }

    public async Task cambiarRolAsync(Guid usuarioId, string nuevoRol, Guid adminId)
    {
        if (usuarioId == Guid.Empty || adminId == Guid.Empty || string.IsNullOrWhiteSpace(nuevoRol))
        {
            throw new ArgumentException("Los datos para cambiar el rol no son válidos.");
        }

       _logger.LogInformation("Cambio de rol para el usuario {usuarioId} a {nuevoRol} por el admin {adminId}", usuarioId, nuevoRol, adminId);

        var usuario = await _repository.GetByIdAsync(usuarioId);
        if(usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        // RF-CA-05 / RF-CA-06: solo un Administrador puede cambiar roles,
        // verificado en el servidor contra el ejecutor (adminId), no el destino.
        var ejecutor = await _repository.GetByIdAsync(adminId);
        if (ejecutor == null || ejecutor.Rol != Rol.Administrador)
        {
            throw new InvalidOperationException("No tiene permiso para cambiar roles.");
        }

        if (!Enum.TryParse<Rol>(nuevoRol, ignoreCase: true, out var rol) || !Enum.IsDefined(rol))
        {
            throw new InvalidOperationException("El rol especificado no es válido");
        }

        var rolAnterior = usuario.Rol;

        usuario.CambiarRol(rol);

        await _repository.UpdateAsync(usuario);

         // RF-CA-08: Auditoría estricta
        await _auditoria.RegistroAuditoriaAsync(
            adminId,
            "Administrador",
            "Cambio de rol",
            "Usuario",
            usuario.Id.ToString(),
            rolAnterior.ToString(),
            rol.ToString()
        );
    }

    public async Task<IEnumerable<UsuarioResponseDto>> ObtenerTodosAsync()
    {
        _logger.LogInformation("Consultando lista de usuarios.");

        var usuarios = await _repository.ObtenerTodosAsync();

        return usuarios.Select(u => new UsuarioResponseDto(
            u.Id,
            u.NombreCompleto,
            u.Correo.Valor,
            u.Rol.ToString(),
            u.Activo
        ));
    }

    public async Task CambiarEstadoAsync(Guid usuarioId, bool nuevoEstado, Guid adminId)
    {
        if (usuarioId == Guid.Empty || adminId == Guid.Empty)
        {
            throw new ArgumentException("Los identificadores de usuario no son válidos.");
        }

        _logger.LogInformation("Cambio de estado para el usuario {usuarioId} a {nuevoEstado} por el admin {adminId}", usuarioId, nuevoEstado, adminId);

        if (usuarioId == adminId)
        {
            throw new InvalidOperationException("Un administrador no puede desactivarse a sí mismo.");
        }

        var usuario = await _repository.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        var estadoAnterior = usuario.Activo;

        if (nuevoEstado)
        {
            usuario.Activar();
        }
        else
        {
            usuario.Desactivar();

            // RF-CA-20: las sesiones abiertas del usuario desactivado dejan de ser válidas.
            _blacklist.RevocarSesionesAnteriores(usuarioId);
        }

        await _repository.UpdateAsync(usuario);

        // RF-CA-08: Auditoría estricta
        await _auditoria.RegistroAuditoriaAsync(
            adminId,
            "Administrador",
            "Cambio de estado",
            "Usuario",
            usuario.Id.ToString(),
            estadoAnterior.ToString(),
            nuevoEstado.ToString()
        );
    }

    public async Task IniciarRecuperacionAsync(string correo)
    {
        var inicio = Stopwatch.GetTimestamp();
        _logger.LogInformation("Iniciando recuperación de contraseña para el correo {correo}", correo);

        var Email = new Email(correo);

        var usuario = await _repository.GetByCorreoAsync(Email);

        // RF-CA-09: respuesta idéntica exista o no el correo, sin revelar registros.
        if (usuario == null)
        {
            await EsperarRespuestaMinimaAsync(inicio);
            return;
        }

        var codigoGenerado = GenerarCodigoRecuperacion();

        usuario.GenerarCodigoRecuperacion(codigoGenerado, 1);

        await _repository.UpdateAsync(usuario);

        await _emailQueue.EncolarCorreoRecuperacionAsync(correo, codigoGenerado);
        await EsperarRespuestaMinimaAsync(inicio);
    }

    public async Task RestablecerPasswordAsync(string correo, string codigo, string nuevaPassword)
    {
        if (string.IsNullOrWhiteSpace(codigo) || codigo.Length > 64)
        {
            throw new ArgumentException("El código de recuperación no es válido.", nameof(codigo));
        }

        _logger.LogInformation("Restablecimiento de contraseña solicitado.");

        ValidarFormatoClave(nuevaPassword);

        var usuario = await _repository.GetByCorreoAsync(new Email(correo));

        // Mensaje uniforme para no revelar qué correos están registrados.
        // Cada código incorrecto cuenta: a los 5 fallos el código se invalida.
        if (usuario == null || usuario.Recuperacion == null || !usuario.Recuperacion.EsValido(codigo))
        {
            if (usuario?.Recuperacion != null)
            {
                usuario.Recuperacion.RegistrarFallo();
                await _repository.UpdateAsync(usuario);
            }

            throw new InvalidOperationException("El código de recuperación es incorrecto, ya fue usado o ha expirado.");
        }

        var nuevoHash = _passwordHasher.Hash(nuevaPassword);

        // RF-CA-11: valida el código (un solo uso + vencimiento) y lo marca como usado.
        usuario.RestablecerPassword(nuevoHash, codigo);

        // RF-CA-12: las sesiones abiertas antes del cambio dejan de ser válidas.
        _blacklist.RevocarSesionesAnteriores(usuario.Id);

        await _repository.UpdateAsync(usuario);
    }

    public async Task CambiarPasswordAsync(Guid usuarioId, string actual, string nueva)
    {
        if (usuarioId == Guid.Empty || string.IsNullOrWhiteSpace(actual) || actual.Length > 1024)
        {
            throw new ArgumentException("Los datos para cambiar la contraseña no son válidos.");
        }
        ValidarFormatoClave(nueva);

        _logger.LogInformation("Cambio de contraseña para el usuario {usuarioId}.", usuarioId);

        var usuario = await _repository.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        if (!_passwordHasher.verificar(usuario.PasswordHash, actual))
        {
            throw new UnauthorizedAccessException("La contraseña actual es incorrecta.");
        }

        // RF-CA-14: formato de la nueva clave.

        usuario.CambiarPassword(_passwordHasher.Hash(nueva));

        // RF-CA-12: las sesiones abiertas antes del cambio dejan de ser válidas.
        _blacklist.RevocarSesionesAnteriores(usuario.Id);

        await _repository.UpdateAsync(usuario);
    }

    public async Task ForzarRestablecimientoAsync(Guid usuarioId, Guid adminId)
    {
        if (usuarioId == Guid.Empty || adminId == Guid.Empty)
        {
            throw new ArgumentException("Los identificadores de usuario no son válidos.");
        }

        _logger.LogInformation("Restablecimiento forzado para el usuario {usuarioId} por el admin {adminId}.", usuarioId, adminId);

        var usuario = await _repository.GetByIdAsync(usuarioId);
        if (usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        // RF-CA-13: la contraseña anterior deja de servir de inmediato.
        usuario.InvalidarPassword();

        // Las sesiones abiertas antes del forzado dejan de ser válidas.
        _blacklist.RevocarSesionesAnteriores(usuario.Id);

        var codigoGenerado = GenerarCodigoRecuperacion();
        usuario.GenerarCodigoRecuperacion(codigoGenerado, 24);

        await _repository.UpdateAsync(usuario);

        await _emailQueue.EncolarCorreoRecuperacionAsync(usuario.Correo.Valor, codigoGenerado);

        // RF-AUD-05: la acción queda auditada.
        await _auditoria.RegistroAuditoriaAsync(
            adminId,
            "Administrador",
            "Restablecimiento forzado de contraseña",
            "Usuario",
            usuario.Id.ToString(),
            "Contraseña anterior activa",
            "Contraseña invalidada (restablecimiento forzado por administrador)"
        );
    }

    private static string GenerarCodigoRecuperacion() =>
        Guid.NewGuid().ToString("N")[..6].ToUpper();

    private static void ValidarFormatoClave(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 8 || password.Length > 1024
            || !password.Any(char.IsLetter) || !password.Any(char.IsDigit))
        {
            throw new ArgumentException("La contraseña debe tener al menos 8 caracteres, e incluir letras y números.");
        }
    }

    private static async Task EsperarRespuestaMinimaAsync(long inicio)
    {
        var restante = TimeSpan.FromMilliseconds(100) - Stopwatch.GetElapsedTime(inicio);
        if (restante > TimeSpan.Zero)
        {
            await Task.Delay(restante);
        }
    }

    public async Task ReenviarEnlaceActivacionAsync(string correo)
    {
        _logger.LogInformation("Solicitud de reenvio de correo para activacion de la cuenta");

        var email = new Email(correo);
        var usuario = await _repository.GetByCorreoAsync(email);

        if(usuario != null && !usuario.Activo)
        {
            string nuevoToken = Guid.NewGuid().ToString("N");
            usuario.EstablecerTokenActivacion(nuevoToken, DateTime.UtcNow.AddHours(24));

            await _repository.UpdateAsync(usuario);
            await _emailQueue.EncolarCorreoActivacionAsync(usuario.Correo.Valor, nuevoToken);
        }
    }

     public async Task ActivarCuentaAsync(string token)
    {
       if (string.IsNullOrWhiteSpace(token) || token.Length > 256)
       {
           throw new ArgumentException("El enlace de activación no es válido.", nameof(token));
       }

       _logger.LogInformation("iniciando activacion de cuenta");

       var usuario = await _repository.GetByTokenRecuperacionAsync(token);

       if(usuario == null)
        {
            throw new InvalidOperationException("El enlace de activacion es incorrecto o ha expirado");
        }

       usuario.CompletarActivacion(token);

       await _repository.UpdateAsync(usuario);
    }

    public async Task<UsuarioResponseDto> RegistrarUsuarioAsync(RegistroRequestDto request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.nombre) || request.nombre.Trim().Length > 200)
        {
            throw new ArgumentException("El nombre completo no es válido.", nameof(request));
        }
        ValidarFormatoClave(request.password);

        _logger.LogInformation("Iniciando registro para el correo {correo}", request.correo);


        // 1. Instancias el Value Object (esto además validará que el correo tenga un formato correcto)
        var Email = new Email(request.correo);

        // 2. verificar correo unico (RF-CA-01)
        var existe = await _repository.CorreoExistsAsync(Email);

        if(existe)
        {
            throw new InvalidOperationException("El correo ya esta registrado");
        }

        // 3. Hashear password antes de crear la entidad (RF-CA-02)
        var passwordhash = _passwordHasher.Hash(request.password);

        // 4. Crear la entidad
        var nuevoUsuario = new Usuario(request.nombre, Email, passwordhash );

        string tokenActivacion = Guid.NewGuid().ToString("N");
        nuevoUsuario.EstablecerTokenActivacion(tokenActivacion, DateTime.UtcNow.AddHours(24));


        // 5. guardar
        await _repository.SaveAsync(nuevoUsuario);

        await _emailQueue.EncolarCorreoActivacionAsync(nuevoUsuario.Correo.Valor, tokenActivacion);

        // 6. mappeamos al dto
        return new UsuarioResponseDto(
            nuevoUsuario.Id, 
            nuevoUsuario.NombreCompleto, 
            nuevoUsuario.Correo.Valor,
            nuevoUsuario.Rol.ToString(),
            nuevoUsuario.Activo
            );
    }
}
