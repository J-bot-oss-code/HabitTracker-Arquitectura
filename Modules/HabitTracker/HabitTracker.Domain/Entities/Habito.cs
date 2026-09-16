namespace HabitTracker.Domain.Entities;

public class Habito
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Frecuencia { get; set; } = string.Empty;  

    public Guid MetaId { get; set; }
    public Meta Meta { get; set; } = null!;

    public ICollection<RegistroDiario> RegistrosDiarios { get; set; } = new List<RegistroDiario>();
    public ICollection<Etiqueta> Etiquetas { get; set; } = new List<Etiqueta>();
    


}