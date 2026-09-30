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
        Task<IEnumerable<UsuarioResponseDto>> ObtenerTodosAsync();
        Task CambiarEstadoAsync(Guid usuarioId, bool nuevoEstado, Guid adminId);
        Task RestablecerPasswordAsync(string correo, string codigo, string nuevaPassword);
        Task CambiarPasswordAsync(Guid usuarioId, string actual, string nueva);
        Task ForzarRestablecimientoAsync(Guid usuarioId, Guid adminId);
        
    }
}