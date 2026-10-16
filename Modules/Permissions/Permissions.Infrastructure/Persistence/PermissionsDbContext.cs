using Microsoft.EntityFrameworkCore;
using Permissions.Domain.Entities;

namespace Permissions.Infrastructure.Persistence;

public sealed class PermissionsDbContext(DbContextOptions<PermissionsDbContext> options) : DbContext(options)
{
    public DbSet<SolicitudPermiso> SolicitudesPermisos => Set<SolicitudPermiso>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PermissionsDbContext).Assembly);
    }
}
