using DocumentManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DocumentManager.Infrastructure.Persistence;

public sealed class DocumentManagerDbContext(DbContextOptions<DocumentManagerDbContext> options) : DbContext(options)
{
    public DbSet<Documento> Documentos => Set<Documento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DocumentManagerDbContext).Assembly);
    }
}
