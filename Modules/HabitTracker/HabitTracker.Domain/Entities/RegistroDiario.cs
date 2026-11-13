namespace HabitTracker.Domain.Entities
{
    /// <summary>Por dónde llegó el avance. Alimenta la métrica «check-in por correo» (Fase 6).</summary>
    public enum OrigenRegistro
    {
        App,
        Correo,
    }

    public class RegistroDiario
    {
        public Guid Id { get; private set; }
        public decimal Cantidad { get; private set; }
        public DateTime Fecha { get; private set; }
        public string Unidad { get; private set; } = string.Empty;
        /// <summary>"Hoy solo lo mínimo": el avance se hizo con la versión mínima de la tarea.</summary>
        public bool EsVersionMinima { get; private set; }
        public OrigenRegistro Origen { get; private set; }
        /// <summary>Hace idempotente un avance que puede llegar dos veces (el check-in por correo: <c>correo:{fechaLocal}</c>).</summary>
        public string? ClaveIdempotencia { get; private set; }

        public Guid HabitoId { get; private set; }
        public Habito Habito { get; private set; } = null!;

        private RegistroDiario() { } // Constructor privado para EF Core

        public const int LongitudClave = 60;

        public RegistroDiario(decimal cantidad, DateTime fechaUtc, string unidad, bool esVersionMinima = false,
            OrigenRegistro origen = OrigenRegistro.App, string? claveIdempotencia = null)
        {
            if (cantidad <= 0)
                throw new ArgumentException("La cantidad debe ser positiva.", nameof(cantidad));
            if (fechaUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("La fecha debe ser UTC.", nameof(fechaUtc));
            if (string.IsNullOrWhiteSpace(unidad))
                throw new ArgumentException("La unidad no puede estar vacía.", nameof(unidad));

            if (!Enum.IsDefined(origen))
                throw new ArgumentException("El origen no es válido.", nameof(origen));
            if (claveIdempotencia is { Length: 0 or > LongitudClave })
                throw new ArgumentException($"La clave debe tener de 1 a {LongitudClave} caracteres.", nameof(claveIdempotencia));

            Id = Guid.NewGuid();
            Cantidad = cantidad;
            Fecha = fechaUtc;
            Unidad = unidad;
            EsVersionMinima = esVersionMinima;
            Origen = origen;
            ClaveIdempotencia = claveIdempotencia;
        }
    }
}
