using AccessControl.Domain.Entities;

namespace AccessControl.Application.Interfaces
{
    public interface IJwtProvider
    {
        string Generar(Usuario usuario);
    }
}
