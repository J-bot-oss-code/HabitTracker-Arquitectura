using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Persistence;

public class HabitTrackerDbContext : DbContext
{
    public HabitTrackerDbContext(DbContextOptions<HabitTrackerDbContext> options) : base(options) { }

    public DbSet<Habito> Habitos { get; set; } = null!;
    public DbSet<Meta> Metas { get; set; } = null!;
    public DbSet<Categoria> Categorias { get; set; } = null!;
    public DbSet<Recompensa> Recompensas { get; set; } = null!;
    public DbSet<RegistroDiario> RegistrosDiarios { get; set; } = null!;
    public DbSet<Etiqueta> Etiquetas { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Habito>()
            .Property(h => h.Estado)
            .HasConversion<string>();

        modelBuilder.Entity<Habito>()
            .HasMany(h => h.Etiquetas)
            .WithMany(e => e.Habitos);
    }
}
