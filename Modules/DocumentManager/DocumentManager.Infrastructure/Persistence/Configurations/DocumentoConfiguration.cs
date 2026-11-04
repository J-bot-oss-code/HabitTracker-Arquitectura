using DocumentManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DocumentManager.Infrastructure.Persistence.Configurations;

public sealed class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    // SQL Server devuelve las fechas con Kind = Unspecified; el convertidor las marca como UTC (RD-11).
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTimeConverter = new(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcDateTimeConverter = new(
        value => value,
        value => value.HasValue
            ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            : null);

    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("Documentos");
        builder.HasKey(documento => documento.Id);

        // El dominio asigna el Id en el constructor; sin esto EF trataría el documento nuevo como existente.
        builder.Property(documento => documento.Id).ValueGeneratedNever();

        builder.Property(documento => documento.NombreOriginal)
            .IsRequired()
            .HasMaxLength(Documento.LongitudMaximaNombre);

        builder.Property(documento => documento.TipoContenido)
            .IsRequired()
            .HasMaxLength(Documento.LongitudMaximaTipo);

        builder.Property(documento => documento.TamanoBytes).IsRequired();

        builder.Property(documento => documento.FechaSubidaUtc)
            .IsRequired()
            .HasColumnType("datetime2")
            .HasConversion(UtcDateTimeConverter);

        builder.Property(documento => documento.UsuarioId).IsRequired();

        builder.Property(documento => documento.CorreoUsuario)
            .IsRequired()
            .HasMaxLength(Documento.LongitudMaximaCorreo);

        builder.Property(documento => documento.NombreAlmacenado)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(documento => documento.Eliminado).IsRequired();

        builder.Property(documento => documento.FechaEliminacionUtc)
            .HasColumnType("datetime2")
            .HasConversion(NullableUtcDateTimeConverter);

        // El listado filtra por dueño y por no eliminado en cada consulta.
        builder.HasIndex(documento => new { documento.UsuarioId, documento.Eliminado });
    }
}
