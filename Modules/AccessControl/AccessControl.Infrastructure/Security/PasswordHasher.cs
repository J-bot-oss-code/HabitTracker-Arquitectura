using AccessControl.Application.Interfaces;

namespace AccessControl.Infrastructure.Security
{

    public class PasswordHasher : IPasswordHasher
    {
        public string Hash(string password)
        {
           return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(12));
        }

        public bool verificar(string hash, string passwordPlana)
        {
            return BCrypt.Net.BCrypt.Verify(hash, passwordPlana);
        }
    }
}