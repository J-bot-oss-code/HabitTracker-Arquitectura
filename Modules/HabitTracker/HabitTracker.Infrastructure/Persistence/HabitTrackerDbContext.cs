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
            .Property(h => h.Periodicidad)
            .HasConversion<string>();

        modelBuilder.Entity<Habito>()
            .Property(h => h.FechaInicioCiclo)
            .HasColumnType("datetime2");

        modelBuilder.Entity<Habito>()
            .HasMany(h => h.Registros)
            .WithOne(r => r.Habito)
            .HasForeignKey(r => r.HabitoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Habito>()
            .HasOne(h => h.Meta)
            .WithMany(m => m.Habitos)
            .HasForeignKey(h => h.MetaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Habito>()
            .HasMany(h => h.Etiquetas)
            .WithMany(e => e.Habitos);

        modelBuilder.Entity<RegistroDiario>()
            .Property(r => r.Fecha)
            .HasColumnType("datetime2");

        // El dominio asigna el Id al crear el registro; sin esto EF trata un avance
        // nuevo con Id ya asignado como existente y emite UPDATE en lugar de INSERT.
        modelBuilder.Entity<RegistroDiario>()
            .Property(r => r.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<Meta>()
            .HasOne(m => m.Categoria)
            .WithMany(c => c.Metas)
            .HasForeignKey(m => m.CategoriaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}