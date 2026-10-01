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
            // BCrypt.Verify(textoPlano, hash): el orden importa; invertido lanza excepción al parsear.
            return BCrypt.Net.BCrypt.Verify(passwordPlana, hash);
        }
    }
}