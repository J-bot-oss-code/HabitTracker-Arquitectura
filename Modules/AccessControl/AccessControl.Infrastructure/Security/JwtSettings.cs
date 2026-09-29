namespace AccessControl.Infrastructure.Security
{
    public sealed class JwtSettings
    {
        public const string Seccion = "JwtSettings";

        public string Clave { get; set; } = string.Empty;
        public string Emisor { get; set; } = string.Empty;
        public string Audiencia { get; set; } = string.Empty;
        public int MinutosExpiracion { get; set; } = 60;
    }
}
