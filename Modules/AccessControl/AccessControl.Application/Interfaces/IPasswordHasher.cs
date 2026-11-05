namespace AccessControl.Application.Interfaces
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool Verificar(string hash, string passwordPlana);
        
    }
}