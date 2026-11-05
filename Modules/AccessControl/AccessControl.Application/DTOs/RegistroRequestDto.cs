using System.ComponentModel.DataAnnotations;

namespace AccessControl.Application.DTOs;

public record RegistroRequestDto(
    [param: Required, StringLength(200, MinimumLength = 1)] string Nombre,
    [param: Required, EmailAddress, StringLength(150)] string Correo,
    // El mínimo de la contraseña lo define y comprueba PoliticaContrasena (responde con el código contrasena_no_valida).
    [param: Required, StringLength(1024)] string Password);
