using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configuraciones;

internal sealed class RegistroDiarioConfiguracion : IEntityTypeConfiguration<RegistroDiario>
{
    public void Configure(EntityTypeBuilder<RegistroDiario> builder)
    {
        // El dominio asigna el Id al crear el registro; sin esto EF trata un avance nuevo con Id ya asignado como
        // existente y emite UPDATE en lugar de INSERT.
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Cantidad).HasPrecision(18, 2);
        builder.Property(r => r.Unidad).HasMaxLength(50).IsRequired();
        builder.Property(r => r.Origen).HasConversion<string>().HasMaxLength(10).HasDefaultValue(OrigenRegistro.App);
        builder.Property(r => r.ClaveIdempotencia).HasMaxLength(RegistroDiario.LongitudClave);
        // El índice filtrado de abajo no sirve para leer todos los registros de un hábito: este sí.
        builder.HasIndex(r => r.HabitoId);
        // Un segundo POST del mismo enlace de correo no duplica el avance: la clave es única por hábito.
        builder.HasIndex(r => new { r.HabitoId, r.ClaveIdempotencia })
            .IsUnique()
            .HasFilter("[ClaveIdempotencia] IS NOT NULL");
    }
}
