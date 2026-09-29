namespace AccessControl.Domain.Entities.ValueObjects
{
    
    public sealed class TokenActivacion
    {
        public string Valor { get; private set; } = null!;
        public DateTime Vencimiento {get; private set;}


        private TokenActivacion() {}

        public TokenActivacion(string valor, DateTime vencimiento)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                throw new ArgumentException("El token de activación no puede estar vacío.", nameof(valor));
            }

            if(vencimiento <= DateTime.UtcNow)
            {
                throw new ArgumentException("El vencimiento debe ser futuro. ", nameof(vencimiento));
            }

            Valor = valor;
            Vencimiento = vencimiento;
        }

        public bool EsValido(string tokenIngresado) => Valor == tokenIngresado && Vencimiento > DateTime.UtcNow;

    }
} 