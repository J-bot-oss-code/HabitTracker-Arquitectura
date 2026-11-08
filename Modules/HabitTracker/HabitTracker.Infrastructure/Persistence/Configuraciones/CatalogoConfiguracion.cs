using HabitTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configuraciones;

/// <summary>Catálogos compartidos (Meta, Categoria, Recompensa, Etiqueta): longitudes acotadas en lugar de nvarchar(max).</summary>
internal sealed class MetaConfiguracion : IEntityTypeConfiguration<Meta>
{
    public void Configure(EntityTypeBuilder<Meta> builder)
    {
        builder.Property(m => m.Titulo).HasMaxLength(200).IsRequired();
        builder.Property(m => m.EstadoActual).HasMaxLength(50).IsRequired();

        builder.HasOne(m => m.Categoria)
            .WithMany(c => c.Metas)
            .HasForeignKey(m => m.CategoriaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CategoriaConfiguracion : IEntityTypeConfiguration<Categoria>
{
    public void Configure(EntityTypeBuilder<Categoria> builder)
    {
        builder.Property(c => c.Nombre).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Descripcion).HasMaxLength(500).IsRequired();
    }
}

internal sealed class RecompensaConfiguracion : IEntityTypeConfiguration<Recompensa>
{
    public void Configure(EntityTypeBuilder<Recompensa> builder)
    {
        builder.Property(r => r.Nombre).HasMaxLength(100).IsRequired();
    }
}

internal sealed class EtiquetaConfiguracion : IEntityTypeConfiguration<Etiqueta>
{
    public void Configure(EntityTypeBuilder<Etiqueta> builder)
    {
        builder.Property(e => e.Nombre).HasMaxLength(50).IsRequired();
        builder.Property(e => e.ColorHex).HasMaxLength(7).IsRequired();
    }
}
