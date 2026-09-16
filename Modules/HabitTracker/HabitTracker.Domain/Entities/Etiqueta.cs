namespace HabitTracker.Domain.Entities
{
    public class Etiqueta
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string ColorHex { get; set; } = "#1a1313";

        public ICollection<Habito> Habitos { get; set; } = new List<Habito>();
    }
}