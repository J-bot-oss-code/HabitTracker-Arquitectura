using AccessControl.Domain.Entities;
using AccessControl.Domain.Entities.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace AccessControl.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options){}

    public DbSet<Usuario> usuarios {get; set;}
    public DbSet<CorreoEnCola> CorreosEnCola { get; set; }

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
           }); 

           entity.OwnsOne(e => e.TokenAcceso, TokenActivacion =>
        {
            TokenActivacion.Property(t => t.Valor)
            .HasColumnName("TokenAcceso")
            .HasMaxLength(200);

            TokenActivacion.Property(t => t.Vencimiento)
            .HasColumnName("Vencimiento");
        }); 

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