using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Persistence.Configurations;

public sealed class NotificacionConfiguration : IEntityTypeConfiguration<Notificacion>
{
    public void Configure(EntityTypeBuilder<Notificacion> builder)
    {
        builder.ToTable("Notificaciones");
        builder.HasKey(n => n.Id);

        // El Id lo asigna el dominio: sin esto EF emitiría un UPDATE en vez de un INSERT.
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.UsuarioId).IsRequired();
        builder.Property(n => n.Tipo).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(n => n.Titulo).HasMaxLength(Notificacion.LongitudMaximaTitulo).IsRequired();
        builder.Property(n => n.Mensaje).HasMaxLength(Notificacion.LongitudMaximaMensaje).IsRequired();
        builder.Property(n => n.CreadaEnUtc).IsRequired();
        builder.Ignore(n => n.Leida);

        // Toda consulta es "las de esta persona", leídas o no leídas, de la más reciente a la más antigua.
        builder.HasIndex(n => new { n.UsuarioId, n.LeidaEnUtc });
    }
}

public sealed class PreferenciaNotificacionesConfiguration : IEntityTypeConfiguration<PreferenciaNotificaciones>
{
    public void Configure(EntityTypeBuilder<PreferenciaNotificaciones> builder)
    {
        builder.ToTable("PreferenciasNotificaciones");
        builder.HasKey(p => p.UsuarioId);
        builder.Property(p => p.UsuarioId).ValueGeneratedNever();
        builder.Property(p => p.CorreoActivo).IsRequired();
    }
}
