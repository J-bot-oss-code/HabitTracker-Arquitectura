namespace HabitTracker.Domain.Entities
{
    public class Habito
    {
        // RD-04: transiciones permitidas declaradas en un único lugar.
        private static readonly IReadOnlyDictionary<EstadoHabito, IReadOnlySet<EstadoHabito>> TransicionesPermitidas =
            new Dictionary<EstadoHabito, IReadOnlySet<EstadoHabito>>
            {
                [EstadoHabito.Pendiente] = new HashSet<EstadoHabito> { EstadoHabito.Activo, EstadoHabito.Abandonado },
                [EstadoHabito.Activo] = new HashSet<EstadoHabito> { EstadoHabito.Pausado, EstadoHabito.Completado, EstadoHabito.Abandonado },
                [EstadoHabito.Pausado] = new HashSet<EstadoHabito> { EstadoHabito.Activo, EstadoHabito.Abandonado },
                [EstadoHabito.Completado] = new HashSet<EstadoHabito>(),
                [EstadoHabito.Abandonado] = new HashSet<EstadoHabito>()
            };

        public Guid Id { get; private set; }
        public string Nombre { get; private set; } = string.Empty;
        public string Frecuencia { get; private set; } = string.Empty;
        public EstadoHabito Estado { get; private set; }

        public Guid MetaId { get; private set; }
        public Meta Meta { get; private set; } = null!;

        public ICollection<RegistroDiario> RegistrosDiarios { get; private set; } = new List<RegistroDiario>();
        public ICollection<Etiqueta> Etiquetas { get; private set; } = new List<Etiqueta>();

        private Habito() {} // Constructor privado para EF Core

        public Habito(string nombre, string frecuencia, Guid metaId)
        {
            if (string.IsNullOrWhiteSpace(nombre))
            {
                throw new ArgumentException("El nombre no puede estar vacío.", nameof(nombre));
            }

            if (string.IsNullOrWhiteSpace(frecuencia))
            {
                throw new ArgumentException("La frecuencia no puede estar vacía.", nameof(frecuencia));
            }

            Id = Guid.NewGuid();
            Nombre = nombre;
            Frecuencia = frecuencia;
            MetaId = metaId;
            Estado = EstadoHabito.Pendiente;
        }

        public void Activar() => TransicionarA(EstadoHabito.Activo);

        public void Pausar() => TransicionarA(EstadoHabito.Pausado);

        public void Completar() => TransicionarA(EstadoHabito.Completado);

        public void Abandonar() => TransicionarA(EstadoHabito.Abandonado);

        private void TransicionarA(EstadoHabito nuevoEstado)
        {
            if (!TransicionesPermitidas[Estado].Contains(nuevoEstado))
            {
                throw new InvalidOperationException($"La transición de {Estado} a {nuevoEstado} está prohibida.");
            }

            Estado = nuevoEstado;
        }
    }
}
