namespace HabitTracker.Domain.Entities
{
    public class RegistroDiario
    {
        public Guid Id { get; set; }
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
        public bool Completado {get; set; } = false;
        public string? Notas { get; set; } = string.Empty;

        public Guid HabitoId { get; set; }
        public Habito Habito { get; set; } = null!;
    }
}