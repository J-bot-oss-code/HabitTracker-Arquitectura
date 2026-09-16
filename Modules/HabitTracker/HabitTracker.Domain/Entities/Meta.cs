namespace HabitTracker.Domain.Entities
{
    public class Meta
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string EstadoActual { get; set; } = string.Empty;
        public DateTime FechaLimite { get; set; }

        public Guid CategoriaId { get; set; }
        public Categoria Categoria { get; set; } = null!;

        public ICollection<Habito> Habitos { get; set; } = new List<Habito>();
        public ICollection<Recompensa> Recompensas { get; set; } = new List<Recompensa>();



        
    }
}