using AccessControl.Application.DTOs;

namespace AccessControl.Application.Interfaces
{
    public interface IUsuarioService
    {
        Task<UsuarioResponseDto> RegistrarUsuarioAsync(RegistroRequestDto request);
        Task<UsuarioResponseDto> AutenticarUsuarioAsync(LoginRequestDto request);
        Task cambiarRolAsync(Guid usuarioId, string nuevoRol, Guid adminId);
        Task IniciarRecuperacionAsync(string correo);

        Task ActivarCuentaAsync(string token);
        Task ReenviarEnlaceActivacionAsync(string correo);
        
    }
}