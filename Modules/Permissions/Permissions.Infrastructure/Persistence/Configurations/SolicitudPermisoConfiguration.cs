using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Permissions.Domain.Entities;

namespace Permissions.Infrastructure.Persistence.Configurations;

public sealed class SolicitudPermisoConfiguration : IEntityTypeConfiguration<SolicitudPermiso>
{
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcDateTimeConverter = new(
        value => value,
        value => value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null);

    public void Configure(EntityTypeBuilder<SolicitudPermiso> builder)
    {
        builder.ToTable("SolicitudesPermisos");
        builder.HasKey(solicitud => solicitud.Id);

        builder.Property(solicitud => solicitud.Id)
            .ValueGeneratedNever();

        builder.Property(solicitud => solicitud.Solicitante)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(solicitud => solicitud.PermisoSolicitado)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(solicitud => solicitud.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(solicitud => solicitud.Aprobador)
            .HasMaxLength(256);

        builder.Property(solicitud => solicitud.FechaCreacion)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasConversion(UtcDateTimeConverter);

        builder.Property(solicitud => solicitud.FechaResolucion)
            .HasColumnType("datetime2")
            .HasConversion(NullableUtcDateTimeConverter);

        builder.Property(solicitud => solicitud.MotivoRechazo)
            .HasMaxLength(500);
    }
}
