using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Planes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configuraciones;

/// <summary>Los planes de las personas (Fase 4). Las pausas son un hijo propio: la racha necesita saber qué días excluir.</summary>
internal sealed class PlanUsuarioConfiguracion : IEntityTypeConfiguration<PlanUsuario>
{
    public void Configure(EntityTypeBuilder<PlanUsuario> builder)
    {
        builder.ToTable("PlanesUsuario");
        builder.HasKey(p => p.Id);
        // El dominio asigna el Id: sin esto EF trataría un plan nuevo como ya existente y emitiría UPDATE.
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Slug).HasMaxLength(PlanUsuario.LongitudSlug);
        builder.Property(p => p.Nombre).HasMaxLength(PlanUsuario.LongitudNombre).IsRequired();
        builder.Property(p => p.Origen).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Estado).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.MotivoPausa).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(p => new { p.UsuarioId, p.Estado });
        // Dos peticiones simultáneas no pueden dejar a una persona con el mismo plan dos veces en curso. Los terminados
        // (completado o cancelado) no cuentan: se puede volver a empezar el mismo plan.
        builder.HasIndex(p => new { p.UsuarioId, p.PlanPlantillaId })
            .IsUnique()
            .HasFilter("[PlanPlantillaId] IS NOT NULL AND [Estado] IN ('Activo', 'Pausado')");

        builder.OwnsMany(p => p.Pausas, pausa =>
        {
            pausa.ToTable("PeriodosPausa");
            pausa.WithOwner().HasForeignKey(x => x.PlanUsuarioId);
            pausa.HasKey(x => x.Id);
            pausa.Property(x => x.Id).ValueGeneratedNever();
            pausa.Property(x => x.Motivo).HasConversion<string>().HasMaxLength(20);
        });
        builder.Navigation(p => p.Pausas).HasField("_pausas").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.SemanasTotales);
        builder.Ignore(p => p.EstaEnCurso);
    }
}

internal sealed class LogroUsuarioConfiguracion : IEntityTypeConfiguration<LogroUsuario>
{
    public void Configure(EntityTypeBuilder<LogroUsuario> builder)
    {
        builder.ToTable("LogrosUsuario");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Clave).HasConversion<string>().HasMaxLength(30);

        // Un logro se gana una sola vez por persona.
        builder.HasIndex(l => new { l.UsuarioId, l.Clave }).IsUnique();
    }
}
