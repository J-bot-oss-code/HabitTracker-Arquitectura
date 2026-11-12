using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Planes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configuraciones;

internal sealed class HabitoConfiguracion : IEntityTypeConfiguration<Habito>
{
    public void Configure(EntityTypeBuilder<Habito> builder)
    {
        // Los mismos límites que valida la API (CrearHabitoRequest).
        builder.Property(h => h.Nombre).HasMaxLength(200).IsRequired();
        builder.Property(h => h.Unidad).HasMaxLength(50).IsRequired();
        builder.Property(h => h.Descripcion).HasMaxLength(TareaPlantilla.LongitudInstrucciones);
        builder.Property(h => h.VersionMinima).HasMaxLength(TareaPlantilla.LongitudVersionMinima);
        builder.Property(h => h.Ancla).HasConversion<string>().HasMaxLength(30);
        builder.Property(h => h.CantidadObjetivo).HasPrecision(18, 2);

        builder.Property(h => h.Estado).HasConversion<string>();
        builder.Property(h => h.Periodicidad).HasConversion<string>();

        builder.HasIndex(h => h.UsuarioId);
        builder.HasIndex(h => h.PlanUsuarioId);

        // Al borrar un plan (baja de cuenta) los hábitos quedan sueltos; la baja los borra aparte.
        builder.HasOne<PlanUsuario>()
            .WithMany()
            .HasForeignKey(h => h.PlanUsuarioId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(h => h.Registros)
            .WithOne(r => r.Habito)
            .HasForeignKey(r => r.HabitoId)
            .OnDelete(DeleteBehavior.Cascade);

        // La meta es opcional. Borrar una meta del catálogo deja sus hábitos sueltos; antes los borraba en cascada.
        builder.HasOne(h => h.Meta)
            .WithMany(m => m.Habitos)
            .HasForeignKey(h => h.MetaId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(h => h.Etiquetas)
            .WithMany(e => e.Habitos);
    }
}
