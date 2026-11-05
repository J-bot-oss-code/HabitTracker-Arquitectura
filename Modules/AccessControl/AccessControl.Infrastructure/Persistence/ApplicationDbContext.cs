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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

            entity.Property(e => e.PasswordHash)
            .IsRequired()
            .HasMaxLength(200);

            entity.Property(e => e.Activo)
            .IsRequired();  
 
        });

        


    }
}