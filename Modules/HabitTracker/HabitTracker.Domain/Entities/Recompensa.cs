namespace HabitTracker.Domain.Entities
{
    public class Recompensa
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int Puntos { get; set; }

        public Guid MetaId { get; set; }
        public Meta Meta { get; set; } = null!;
    }
}