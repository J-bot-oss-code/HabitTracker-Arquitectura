using Auditing.Domain;
using Microsoft.EntityFrameworkCore;

namespace Auditing.Infrastructure.Persistence;

/// <summary>
/// Contexto del historial de auditoría. Es de solo agregar (RF-AUD-06): cualquier intento de guardar un registro modificado o
/// eliminado se rechaza aquí, aunque alguien lo intentara desde otro punto del código.
/// </summary>
public sealed class AuditingDbContext(DbContextOptions<AuditingDbContext> options) : DbContext(options)
{
    public const string MensajeInmutable = "El historial de auditoría no se modifica ni se elimina.";

    public DbSet<RegistroAuditoria> Registros => Set<RegistroAuditoria>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditingDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        RechazarModificaciones();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        RechazarModificaciones();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void RechazarModificaciones()
    {
        if (ChangeTracker.Entries().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(MensajeInmutable);
        }
    }
}
