using AccessControl.Application.Interfaces;

namespace AccessControl.Infrastructure.Security
{

    public class PasswordHasher : IPasswordHasher
    {
        private const int FactorDeCosto = 12;

        // Hash válido de una clave que nadie conoce, calculado una vez con el mismo costo que los reales.
        private static readonly string HashFicticio =
            BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString("N"), BCrypt.Net.BCrypt.GenerateSalt(FactorDeCosto));

        public void SimularVerificacion(string passwordPlana)
        {
            _ = Verificar(HashFicticio, passwordPlana);
        }

        public string Hash(string password)
        {
           return BCrypt.Net.BCrypt.HashPassword(password, BCrypt.Net.BCrypt.GenerateSalt(FactorDeCosto));
        }

        public bool Verificar(string hash, string passwordPlana)
        {
            // BCrypt.Verify(textoPlano, hash): el orden importa; invertido lanza excepción al parsear.
            // Un hash corrupto o con formato inválido nunca hace match: se devuelve false en vez de propagar.
            try
            {
                return BCrypt.Net.BCrypt.Verify(passwordPlana, hash);
            }
            catch
            {
                return false;
            }
        }
    }
}