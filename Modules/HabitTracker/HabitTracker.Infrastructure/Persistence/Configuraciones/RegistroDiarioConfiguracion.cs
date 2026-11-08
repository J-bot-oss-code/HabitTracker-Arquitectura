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
    }
}
