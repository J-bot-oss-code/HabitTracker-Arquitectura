using System.ComponentModel.DataAnnotations;

namespace AccessControl.Application.DTOs;

// En JSON el campo del correo se llama "email" (así lo documentan el README y el plan de pruebas): no se renombra.
public record LoginRequestDto(
    [param: Required, EmailAddress, StringLength(150)] string Email,
    [param: Required, StringLength(1024)] string Password);
