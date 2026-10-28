namespace HabitTracker.Domain.Entities
{
    public class RegistroDiario
    {
        public Guid Id { get; private set; }
        public decimal Cantidad { get; private set; }
        public DateTime Fecha { get; private set; }
        public string Unidad { get; private set; } = string.Empty;

        public Guid HabitoId { get; private set; }
        public Habito Habito { get; private set; } = null!;

        private RegistroDiario() { } // Constructor privado para EF Core

        public RegistroDiario(decimal cantidad, DateTime fechaUtc, string unidad)
        {
            if (cantidad <= 0)
                throw new ArgumentException("La cantidad debe ser positiva.", nameof(cantidad));
            if (fechaUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("La fecha debe ser UTC.", nameof(fechaUtc));
            if (string.IsNullOrWhiteSpace(unidad))
                throw new ArgumentException("La unidad no puede estar vacía.", nameof(unidad));

            Id = Guid.NewGuid();
            Cantidad = cantidad;
            Fecha = fechaUtc;
            Unidad = unidad;
        }
    }
}
