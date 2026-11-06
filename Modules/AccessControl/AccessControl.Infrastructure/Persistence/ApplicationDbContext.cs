using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options){}

    public DbSet<Usuario> usuarios {get; set;}
    public DbSet<CorreoEnCola> CorreosEnCola { get; set; }
    public DbSet<TokenRevocado> TokensRevocados { get; set; } = null!;
    public DbSet<RevocacionSesion> RevocacionesSesion { get; set; } = null!;
    public DbSet<ConsentimientoUsuario> ConsentimientosUsuario { get; set; } = null!;
    public DbSet<SesionUsuario> SesionesUsuario { get; set; } = null!;
    public DbSet<LoginExterno> LoginsExternos { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Historial de solo inserción de lo que cada usuario aceptó (documentos legales y consentimientos opcionales).
        modelBuilder.Entity<ConsentimientoUsuario>(entity =>
        {
            entity.ToTable("ConsentimientosUsuario");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.TerminosVersion).IsRequired().HasMaxLength(20);
            entity.Property(c => c.PrivacidadVersion).IsRequired().HasMaxLength(20);
            entity.Property(c => c.Origen).IsRequired().HasMaxLength(20);

            // Al eliminar una cuenta se eliminan también sus consentimientos (baja de cuenta, fase 6).
            entity.HasOne<Usuario>().WithMany().HasForeignKey(c => c.UsuarioId).OnDelete(DeleteBehavior.Cascade);

            // La consulta típica es "el último consentimiento de este usuario".
            entity.HasIndex(c => new { c.UsuarioId, c.ActualizadoEn });
        });

        // Sesiones con refresh token rotativo. Solo se guarda el hash del token; ReemplazadaPorId no es clave foránea a propósito
        // (la fila nueva y la revocación de la anterior se escriben en la misma transacción y la limpieza borra por antigüedad).
        modelBuilder.Entity<SesionUsuario>(entity =>
        {
            entity.ToTable("SesionesUsuario");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.TokenHash).IsRequired().HasMaxLength(SesionUsuario.LongitudHash);
            entity.Property(x => x.Dispositivo).IsRequired().HasMaxLength(SesionUsuario.LongitudMaximaDispositivo);
            entity.Property(x => x.IpCreacion).HasMaxLength(SesionUsuario.LongitudMaximaIp);

            // Al eliminar una cuenta se eliminan también sus sesiones (baja de cuenta, fase 6).
            entity.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UsuarioId);
            entity.HasIndex(x => x.FamiliaId);
        });

        // Vínculos con proveedores externos (Google). Lo que identifica a la persona es (Proveedor, Subject), no el correo.
        modelBuilder.Entity<LoginExterno>(entity =>
        {
            entity.ToTable("LoginsExternos");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Proveedor).IsRequired().HasMaxLength(20);
            entity.Property(x => x.Subject).IsRequired().HasMaxLength(LoginExterno.LongitudMaximaSubject);

            // Al eliminar una cuenta se eliminan también sus vínculos (baja de cuenta, fase 6).
            entity.HasOne<Usuario>().WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Cascade);

            // Una identidad del proveedor pertenece a una sola cuenta, y una cuenta tiene una sola identidad por proveedor.
            entity.HasIndex(x => new { x.Proveedor, x.Subject }).IsUnique();
            entity.HasIndex(x => new { x.UsuarioId, x.Proveedor }).IsUnique();
        });

        modelBuilder.Entity<Usuario>(entity =>
        {
           entity.HasKey(e => e.Id);

           entity.OwnsOne(e => e.Correo, Email =>
           {
               Email.Property(e => e.Valor)
               .HasColumnName("Correo")
               .IsRequired()
               .HasMaxLength(150);

               // RF-CA-01: el correo es único también a nivel de base de datos. Sin el índice, dos registros simultáneos
               // con el mismo correo pasaban la comprobación previa y creaban dos cuentas (carrera "comprobar y luego insertar").
               // El valor ya llega en minúsculas desde el objeto de valor Email.
               Email.HasIndex(e => e.Valor)
               .IsUnique()
               .HasDatabaseName("IX_usuarios_Correo");
           });

            entity.OwnsOne(e => e.TokenAcceso, TokenActivacion =>
        {
            TokenActivacion.Property(t => t.Valor)
            .HasColumnName("TokenAcceso")
            .HasMaxLength(200);

            TokenActivacion.Property(t => t.Vencimiento)
            .HasColumnName("Vencimiento");

            // Intentos fallidos con el código de activación (límite de fuerza bruta). Columna anulable: ver TokenActivacion.
            TokenActivacion.Property<int?>("_intentosFallidos")
            .HasColumnName("IntentosActivacion");
        });

            entity.OwnsOne(u => u.Bloqueo);

            entity.OwnsOne(u => u.Recuperacion);

        entity.Property(e => e.NombreCompleto)
            .IsRequired()
            .HasMaxLength(200);

            // Nulo en las cuentas creadas con Google (no tienen contraseña).
            entity.Property(e => e.PasswordHash)
            .HasMaxLength(200);

            entity.Property(e => e.Activo)
            .IsRequired();  
 
        });

        


    }
}