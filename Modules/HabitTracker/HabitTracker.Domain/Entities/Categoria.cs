namespace HabitTracker.Domain.Entities
{
    public class Categoria
    {
        public Guid Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;

        public ICollection<Meta> Metas { get; set; } = new List<Meta>();
        
    }
}