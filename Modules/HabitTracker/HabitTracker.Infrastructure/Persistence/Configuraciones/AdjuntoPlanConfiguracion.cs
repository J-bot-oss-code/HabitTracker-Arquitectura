using HabitTracker.Domain.Planes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configuraciones;

/// <summary>Archivos vinculados a un plan. Caen en cascada con el plan (baja de cuenta).</summary>
internal sealed class AdjuntoPlanConfiguracion : IEntityTypeConfiguration<AdjuntoPlan>
{
    public void Configure(EntityTypeBuilder<AdjuntoPlan> builder)
    {
        builder.ToTable("AdjuntosPlan");
        builder.HasKey(a => a.Id);
        // El dominio asigna el Id: sin esto EF trataría un vínculo nuevo como ya existente y emitiría UPDATE.
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.HasOne<PlanUsuario>().WithMany().HasForeignKey(a => a.PlanUsuarioId).OnDelete(DeleteBehavior.Cascade);

        // El mismo archivo no se vincula dos veces al mismo plan, ni con dos peticiones a la vez.
        builder.HasIndex(a => new { a.PlanUsuarioId, a.DocumentoId }).IsUnique();
        // Para saber si un archivo está en algún plan de esa persona (la guarda de eliminar).
        builder.HasIndex(a => new { a.UsuarioId, a.DocumentoId });
    }
}
