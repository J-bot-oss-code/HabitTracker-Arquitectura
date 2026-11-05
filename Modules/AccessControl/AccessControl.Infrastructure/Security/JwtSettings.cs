namespace AccessControl.Infrastructure.Security
{
    public sealed class JwtSettings
    {
        public const string Seccion = "JwtSettings";

        public string Clave { get; set; } = string.Empty;
        public string Emisor { get; set; } = string.Empty;
        public string Audiencia { get; set; } = string.Empty;
        /// <summary>Vida del token de acceso. Es corta a propósito: la sesión larga la sostiene el refresh token.</summary>
        public int MinutosExpiracion { get; set; } = 15;

        /// <summary>
        /// Única validación de la configuración JWT. Se ejecuta al arrancar la aplicación (falla en el arranque y no
        /// en el primer inicio de sesión) y al emitir cada token.
        /// </summary>
        public void Validar()
        {
            if (string.IsNullOrWhiteSpace(Clave) || System.Text.Encoding.UTF8.GetBytes(Clave).Length < 32)
            {
                throw new InvalidOperationException("Falta configuración JWT válida (JwtSettings:Clave de al menos 32 caracteres).");
            }

            if (string.IsNullOrWhiteSpace(Emisor) || string.IsNullOrWhiteSpace(Audiencia))
            {
                throw new InvalidOperationException("Falta configuración JWT válida (JwtSettings:Emisor y JwtSettings:Audiencia).");
            }

            if (MinutosExpiracion <= 0)
            {
                throw new InvalidOperationException("La configuración JWT no es válida (JwtSettings:MinutosExpiracion debe ser mayor a cero).");
            }
        }
    }
}
