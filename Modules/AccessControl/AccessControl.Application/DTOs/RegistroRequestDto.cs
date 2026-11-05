using System.ComponentModel.DataAnnotations;

namespace AccessControl.Application.DTOs;

public record RegistroRequestDto(
    [param: Required, StringLength(200, MinimumLength = 1)] string Nombre,
    [param: Required, EmailAddress, StringLength(150)] string Correo,
    [param: Required, StringLength(1024, MinimumLength = 8)] string Password);
