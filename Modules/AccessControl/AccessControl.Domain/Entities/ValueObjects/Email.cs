namespace AccessControl.Domain.Entities.ValueObjects
{
    public sealed class Email 
    {
        public string Valor { get; private set; } = string.Empty;

        private Email() { }

        public Email(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("El correo electrónico no puede estar vacío.", nameof(email));
            }

            if (!EsFormatoValido(email))
            {
                throw new ArgumentException("El formato del correo electrónico no es válido.", nameof(email));
            }

            Valor = email.ToLowerInvariant();
        }

        private static bool EsFormatoValido(string email)
        {
           
                var parts = email.Split('@');
                if (parts.Length != 2) return false;
                if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1])) return false;
                if (!parts[1].Contains('.')) return false;

                return true;
                
        }

        public static implicit operator string(Email email) => email.Valor; // esto permite que un objeto Email se pueda usar como string directamente

        public override bool Equals(object? obj) => obj is Email other && Valor == other.Valor; // esto permite comparar dos objetos Email directamente

        public override int GetHashCode() => Valor.GetHashCode(); // esto permite que un objeto Email se pueda usar en colecciones que dependen de hash codes, como diccionarios o conjuntos

        public override string ToString() => Valor; // esto permite que un objeto Email se pueda convertir a string directamente, por ejemplo al imprimirlo
    }
}
