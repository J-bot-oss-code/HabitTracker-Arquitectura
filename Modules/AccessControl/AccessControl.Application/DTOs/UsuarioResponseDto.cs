namespace AccessControl.Application.DTOs
{
    public record UsuarioResponseDto(Guid id, string nombre, string correo, string rol, bool Activo, string? Token = null);
    
}