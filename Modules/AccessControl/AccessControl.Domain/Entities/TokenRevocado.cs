namespace AccessControl.Domain.Entities
{
    public class TokenRevocado
    {
        public Guid Id { get; private set; }
        public string TokenHash { get; private set; } = null!;
        public DateTime FechaExpiracion { get; private set; }

        private TokenRevocado() {} // Constructor privado para EF Core

        public TokenRevocado(string tokenHash, DateTime fechaExpiracion)
        {
            if (string.IsNullOrWhiteSpace(tokenHash))
            {
                throw new ArgumentException("El hash del token no puede estar vacío.", nameof(tokenHash));
            }

            Id = Guid.NewGuid();
            TokenHash = tokenHash;
            FechaExpiracion = fechaExpiracion;
        }
    }
}
