namespace AccessControl.Application.Interfaces
{
    public interface ITokenBlacklist
    {
        void InvalidarToken(string token);
        bool EstaInvalidado(string token);
        void RevocarUsuario(Guid usuarioId);
        bool UsuarioRevocado(Guid usuarioId);
    }
}
