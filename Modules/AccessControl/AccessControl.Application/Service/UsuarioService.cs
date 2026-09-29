using AccessControl.Application.DTOs;
using AccessControl.Application.Interfaces;
using AccessControl.Domain.Interfaces;
using AccessControl.Domain.Entities.ValueObjects;
using Microsoft.Extensions.Logging;
using AccessControl.Domain.Entities;

namespace AccessControl.Application.Service;

public class UsuarioService : IUsuarioService
{
    private readonly IUsuarioRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailQueue _emailQueue;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<UsuarioService> _logger;
    public UsuarioService(
        IUsuarioRepository repository, 
        IPasswordHasher passwordHasher, 
        IEmailQueue emailQueue,
        IAuditoriaService auditoria,
        ILogger<UsuarioService> logger)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _emailQueue = emailQueue;
        _auditoria = auditoria;
        _logger = logger;
    }

   

    public async Task<UsuarioResponseDto> AutenticarUsuarioAsync(LoginRequestDto request)
    {
        _logger.LogInformation("Intento de login para: {Email}", request.Email);
        
        var Email = new Email(request.Email);

        var usuario = await _repository.GetByCorreoAsync(Email);

        // RF-CA-03: Rechaso explisito sin revelar que fallo
        if(usuario == null || !_passwordHasher.verificar(usuario.PasswordHash, request.password))
        {
            throw new UnauthorizedAccessException("Credenciales incorrectas");

        }

        return new UsuarioResponseDto(
            usuario.Id,
            usuario.NombreCompleto, 
            usuario.Correo.Valor,
            usuario.Rol.ToString(),
            usuario.Activo
        );



    }

    public async Task cambiarRolAsync(Guid usuarioId, string nuevoRol, Guid adminId)
    {
       _logger.LogInformation("Cambio de rol para el usuario {usuarioId} a {nuevoRol} por el admin {adminId}", usuarioId, nuevoRol, adminId);

        var usuario = await _repository.GetByIdAsync(usuarioId);
        if(usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        if(!Enum.TryParse<Rol>(nuevoRol, out var rol))
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

    public async Task IniciarRecuperacionAsync(string correo)
    {
        _logger.LogInformation("Iniciando recuperación de contraseña para el correo {correo}", correo);

        var Email = new Email(correo);

        var usuario = await _repository.GetByCorreoAsync(Email);

        if(usuario == null)
        {
            throw new InvalidOperationException("El usuario no existe");
        }

        // Generamos un código temporal (podría ser un Guid o código numérico)
        var codigoGenerado = Guid.NewGuid().ToString("N")[..8].ToUpper(); // esto genera un código de 8 caracteres alfanuméricos
        

        // RF-CA-09 al 13: Simulamos encolar el correo sin importar si el usuario existe o no
        // para no revelar qué correos están registrados.
        await _emailQueue.EncolarCorreoRecuperacionAsync(correo, codigoGenerado);

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
        _logger.LogInformation("Iniciando registro para el correo {correo}", request.correo);

        if (request.password.Length < 8 || !request.password.Any(char.IsLetter) || !request.password.Any(char.IsDigit))
        {
            throw new ArgumentException("la contraseña debe tener al menos 8 caracteres, e incluir letras y números.");
        }

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
