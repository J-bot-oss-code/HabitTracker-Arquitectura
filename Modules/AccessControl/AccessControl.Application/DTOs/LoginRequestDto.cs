using System.ComponentModel.DataAnnotations;

namespace AccessControl.Application.DTOs;

public record LoginRequestDto(
    [param: Required, EmailAddress, StringLength(150)] string Email,
    [param: Required, StringLength(1024)] string password);
