using HabitTracker.Domain.Sugerencias;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configuraciones;

internal sealed class SugerenciaConfiguracion : IEntityTypeConfiguration<Sugerencia>
{
    public void Configure(EntityTypeBuilder<Sugerencia> builder)
    {
        builder.ToTable("Sugerencias");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Tipo).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(s => s.Estado).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(s => s.Texto).HasMaxLength(Sugerencia.LongitudMaxima).IsRequired();

        // La bandeja del Administrador filtra por estado y ordena por fecha; «Mis sugerencias» y el tope diario, por persona.
        builder.HasIndex(s => new { s.Estado, s.CreadaEnUtc });
        builder.HasIndex(s => new { s.UsuarioId, s.CreadaEnUtc });
    }
}
