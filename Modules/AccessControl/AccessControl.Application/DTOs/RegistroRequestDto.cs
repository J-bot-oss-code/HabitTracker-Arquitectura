using System.ComponentModel.DataAnnotations;

namespace AccessControl.Application.DTOs;

public record RegistroRequestDto(
    [property: Required, StringLength(200, MinimumLength = 1)] string nombre,
    [property: Required, EmailAddress, StringLength(150)] string correo,
    [property: Required, StringLength(1024, MinimumLength = 8)] string password);
