using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Domain.Progreso;
using HabitTracker.Infrastructure.Persistence.Configuraciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

    // Fase 2 de HabitTracker v2: perfil, salud (cifrada) y progreso corporal.
    public DbSet<PerfilUsuario> PerfilesUsuario { get; set; } = null!;
    internal DbSet<CuestionarioSaludFila> CuestionariosSalud { get; set; } = null!;
    public DbSet<RegistroPeso> RegistrosPeso { get; set; } = null!;
    public DbSet<MedicionCorporal> MedicionesCorporales { get; set; } = null!;

    // Fase 3: catálogo de planes (lo escribe solo el sembrador al arrancar).
    public DbSet<PlanPlantilla> PlanesPlantilla { get; set; } = null!;

    // Fase 4: los planes de cada persona, su constancia y sus logros.
    public DbSet<PlanUsuario> PlanesUsuario { get; set; } = null!;
    public DbSet<LogroUsuario> LogrosUsuario { get; set; } = null!;
    public DbSet<AdjuntoPlan> AdjuntosPlan { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Una clase IEntityTypeConfiguration por entidad, en Persistence/Configuraciones (RD-01).
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HabitTrackerDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // RD-11: el módulo guarda todas las fechas en UTC. SQL Server (datetime2) no guarda el Kind y las devuelve como
        // Unspecified, así que la API las serializaba sin "Z". Este conversor las marca como UTC al leerlas.
        configurationBuilder.Properties<DateTime>()
            .HaveConversion<FechaUtcConverter>()
            .HaveColumnType("datetime2");
        configurationBuilder.Properties<DateTime?>()
            .HaveConversion<FechaUtcNullableConverter>()
            .HaveColumnType("datetime2");
    }

    // Al escribir, una fecha Local se pasa a UTC (las del dominio ya llegan en UTC); al leer, se marca como UTC.
    private sealed class FechaUtcConverter() : ValueConverter<DateTime, DateTime>(
        fecha => fecha.Kind == DateTimeKind.Local ? fecha.ToUniversalTime() : fecha,
        fecha => DateTime.SpecifyKind(fecha, DateTimeKind.Utc));

    private sealed class FechaUtcNullableConverter() : ValueConverter<DateTime?, DateTime?>(
        fecha => fecha.HasValue && fecha.Value.Kind == DateTimeKind.Local ? fecha.Value.ToUniversalTime() : fecha,
        fecha => fecha.HasValue ? DateTime.SpecifyKind(fecha.Value, DateTimeKind.Utc) : fecha);
}
