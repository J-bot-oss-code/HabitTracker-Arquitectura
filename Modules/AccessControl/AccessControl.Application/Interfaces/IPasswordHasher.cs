namespace AccessControl.Application.Interfaces
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool verificar(string hash, string passwordPlana);
        
    }
}