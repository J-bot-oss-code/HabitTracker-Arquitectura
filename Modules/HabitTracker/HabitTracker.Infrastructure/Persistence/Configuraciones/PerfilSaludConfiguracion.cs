using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Progreso;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configuraciones;

internal sealed class PerfilUsuarioConfiguracion : IEntityTypeConfiguration<PerfilUsuario>
{
    public void Configure(EntityTypeBuilder<PerfilUsuario> builder)
    {
        builder.ToTable("PerfilesUsuario");
        builder.HasKey(p => p.UsuarioId);
        // La clave es el usuario de Control de Acceso: nunca la genera la base de datos.
        builder.Property(p => p.UsuarioId).ValueGeneratedNever();

        builder.Property(p => p.PerfilLaboral).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.ZonaHoraria).HasMaxLength(64).IsRequired();
        builder.Property(p => p.Sexo).HasConversion<string>().HasMaxLength(10);
        builder.Property(p => p.Actividad).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.AlturaCm).HasPrecision(6, 2);
        builder.Property(p => p.PesoKg).HasPrecision(6, 2);
        builder.Property(p => p.ObjetivoPesoKg).HasPrecision(6, 2);
        builder.Ignore(p => p.Imc);
    }
}

/// <summary>
/// Fila de persistencia del cuestionario de salud: cada bloque va cifrado (JSON protegido con Data Protection) y nunca se
/// filtra por su contenido en SQL. El dominio (<c>CuestionarioSalud</c>) no sabe nada del cifrado (RD-01).
/// </summary>
internal sealed class CuestionarioSaludFila
{
    public Guid UsuarioId { get; set; }
    public int Version { get; set; }
    public string? ObjetivosCifrado { get; set; }
    public DateTime? ObjetivosRespondidoEn { get; set; }
    public string? SeguridadCifrado { get; set; }
    public DateTime? SeguridadRespondidoEn { get; set; }
    public string? CondicionesCifrado { get; set; }
    public DateTime? CondicionesRespondidoEn { get; set; }
}

internal sealed class CuestionarioSaludFilaConfiguracion : IEntityTypeConfiguration<CuestionarioSaludFila>
{
    public void Configure(EntityTypeBuilder<CuestionarioSaludFila> builder)
    {
        builder.ToTable("CuestionariosSalud");
        builder.HasKey(c => c.UsuarioId);
        builder.Property(c => c.UsuarioId).ValueGeneratedNever();
        builder.Property(c => c.ObjetivosCifrado).HasMaxLength(4000);
        builder.Property(c => c.SeguridadCifrado).HasMaxLength(4000);
        builder.Property(c => c.CondicionesCifrado).HasMaxLength(4000);
    }
}

internal sealed class RegistroPesoConfiguracion : IEntityTypeConfiguration<RegistroPeso>
{
    public void Configure(EntityTypeBuilder<RegistroPeso> builder)
    {
        builder.ToTable("RegistrosPeso");
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.PesoKg).HasPrecision(6, 2);
        builder.HasIndex(r => new { r.UsuarioId, r.Fecha });
    }
}

internal sealed class MedicionCorporalConfiguracion : IEntityTypeConfiguration<MedicionCorporal>
{
    public void Configure(EntityTypeBuilder<MedicionCorporal> builder)
    {
        builder.ToTable("MedicionesCorporales");
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Zona).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Valor).HasPrecision(6, 2);
        builder.HasIndex(m => new { m.UsuarioId, m.Zona, m.Fecha });
    }
}
