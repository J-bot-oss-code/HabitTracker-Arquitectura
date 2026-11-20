using Auditing.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Auditing.Infrastructure.Persistence.Configurations;

public sealed class RegistroAuditoriaConfiguration : IEntityTypeConfiguration<RegistroAuditoria>
{
    public void Configure(EntityTypeBuilder<RegistroAuditoria> builder)
    {
        builder.ToTable("RegistrosAuditoria");
        builder.HasKey(r => r.Id);

        // El Id lo asigna el dominio: sin esto EF emitiría un UPDATE en vez de un INSERT.
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.UsuarioId).IsRequired();
        builder.Property(r => r.Usuario).HasMaxLength(RegistroAuditoria.LongitudMaximaUsuario).IsRequired();
        builder.Property(r => r.Accion).HasMaxLength(RegistroAuditoria.LongitudMaximaAccion).IsRequired();
        builder.Property(r => r.Entidad).HasMaxLength(RegistroAuditoria.LongitudMaximaEntidad).IsRequired();
        builder.Property(r => r.EntidadId).HasMaxLength(RegistroAuditoria.LongitudMaximaEntidadId).IsRequired();
        builder.Property(r => r.FechaUtc).IsRequired();
        builder.Property(r => r.ValorAnterior).HasMaxLength(RegistroAuditoria.LongitudMaximaValor).IsRequired();
        builder.Property(r => r.ValorNuevo).HasMaxLength(RegistroAuditoria.LongitudMaximaValor).IsRequired();

        // El historial crece sin parar y se consulta por rango de fechas, por quién actuó y por entidad: un índice para cada filtro.
        builder.HasIndex(r => r.FechaUtc);
        builder.HasIndex(r => new { r.Usuario, r.FechaUtc });
        builder.HasIndex(r => new { r.Entidad, r.EntidadId, r.FechaUtc });
    }
}
