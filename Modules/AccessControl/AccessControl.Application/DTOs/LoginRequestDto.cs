using System.ComponentModel.DataAnnotations;

namespace AccessControl.Application.DTOs;

public record LoginRequestDto(
    [property: Required, EmailAddress, StringLength(150)] string Email,
    [property: Required, StringLength(1024)] string password);
