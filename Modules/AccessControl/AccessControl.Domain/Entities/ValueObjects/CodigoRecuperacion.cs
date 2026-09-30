namespace AccessControl.Domain.Entities.ValueObjects
{
    public sealed class CodigoRecuperacion
    {
        public string Valor { get; private set; } = null!;
        public DateTime FechaEmision { get; private set; }
        public DateTime FechaVencimiento { get; private set; }
        public bool Usado { get; private set; }

        private CodigoRecuperacion() {} // Constructor privado para EF Core

        public CodigoRecuperacion(string valor, DateTime fechaVencimiento)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                throw new ArgumentException("El código de recuperación no puede estar vacío.", nameof(valor));
            }

            if (fechaVencimiento <= DateTime.UtcNow)
            {
                throw new ArgumentException("El vencimiento debe ser futuro.", nameof(fechaVencimiento));
            }

            Valor = valor;
            FechaEmision = DateTime.UtcNow;
            FechaVencimiento = fechaVencimiento;
            Usado = false;
        }

        public void MarcarComoUsado()
        {
            if (Usado)
            {
                throw new InvalidOperationException("Este código ya fue utilizado.");
            }

            Usado = true;
        }

        public bool EsValido(string codigo) =>
            !Usado && Valor == codigo && FechaVencimiento > DateTime.UtcNow;
    }
}
