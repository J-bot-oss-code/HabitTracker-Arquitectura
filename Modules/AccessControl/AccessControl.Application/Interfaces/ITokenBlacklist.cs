namespace AccessControl.Application.Interfaces
{
    public interface ITokenBlacklist
    {
        void InvalidarToken(string token);
        bool EstaInvalidado(string token);
        void RevocarSesionesAnteriores(Guid usuarioId);
        bool SesionVigente(Guid usuarioId, DateTimeOffset emitidoEn);
    }
}
