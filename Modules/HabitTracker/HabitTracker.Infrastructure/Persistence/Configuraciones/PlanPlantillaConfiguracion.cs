using HabitTracker.Domain.Planes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HabitTracker.Infrastructure.Persistence.Configuraciones;

/// <summary>
/// Catálogo de planes (Fase 3). Las listas de valores (jornadas, zonas, contraindicaciones, alérgenos, beneficios y guía)
/// van como JSON en su columna: se leen siempre con el plan y nunca se filtran en SQL. Las tareas van en su tabla.
/// </summary>
internal sealed class PlanPlantillaConfiguracion : IEntityTypeConfiguration<PlanPlantilla>
{
    private const int LongitudEnum = 30;

    public void Configure(EntityTypeBuilder<PlanPlantilla> builder)
    {
        builder.ToTable("PlanesPlantilla");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Slug).HasMaxLength(PlanPlantilla.LongitudSlug).IsRequired();
        builder.HasIndex(p => p.Slug).IsUnique();
        builder.HasIndex(p => p.Activo);
        builder.Property(p => p.Nombre).HasMaxLength(PlanPlantilla.LongitudNombre).IsRequired();
        builder.Property(p => p.Resumen).HasMaxLength(PlanPlantilla.LongitudResumen).IsRequired();
        builder.Property(p => p.Descripcion).HasMaxLength(PlanPlantilla.LongitudDescripcion).IsRequired();
        builder.Property(p => p.AvisoSeguridad).HasMaxLength(PlanPlantilla.LongitudAviso).IsRequired();

        builder.Property(p => p.Area).HasConversion<string>().HasMaxLength(LongitudEnum);
        builder.Property(p => p.Nivel).HasConversion<string>().HasMaxLength(LongitudEnum);
        builder.Property(p => p.Intensidad).HasConversion<string>().HasMaxLength(LongitudEnum);
        builder.Property(p => p.Sexo).HasConversion<string>().HasMaxLength(LongitudEnum);
        builder.Property(p => p.Lugar).HasConversion<string>().HasMaxLength(LongitudEnum);

        builder.PrimitiveCollection(p => p.PerfilesLaborales).ElementType().HasConversion<string>();
        builder.PrimitiveCollection(p => p.ZonasCorporales).ElementType().HasConversion<string>();
        builder.PrimitiveCollection(p => p.Contraindicaciones).ElementType().HasConversion<string>();
        builder.PrimitiveCollection(p => p.Alergenos).ElementType().HasConversion<string>();
        builder.PrimitiveCollection(p => p.Beneficios);

        builder.OwnsMany(p => p.GuiaSemanal, guia => guia.ToJson());

        builder.OwnsMany(p => p.Tareas, tarea =>
        {
            tarea.ToTable("TareasPlantilla");
            tarea.WithOwner().HasForeignKey("PlanPlantillaId");
            tarea.HasKey(t => t.Id);
            tarea.Property(t => t.Id).ValueGeneratedNever();
            tarea.Property(t => t.Nombre).HasMaxLength(TareaPlantilla.LongitudNombre).IsRequired();
            tarea.Property(t => t.Instrucciones).HasMaxLength(TareaPlantilla.LongitudInstrucciones).IsRequired();
            tarea.Property(t => t.Unidad).HasMaxLength(TareaPlantilla.LongitudUnidad).IsRequired();
            tarea.Property(t => t.VersionMinima).HasMaxLength(TareaPlantilla.LongitudVersionMinima).IsRequired();
            tarea.Property(t => t.Periodicidad).HasConversion<string>().HasMaxLength(LongitudEnum);
            tarea.Property(t => t.AnclaSugerida).HasConversion<string>().HasMaxLength(LongitudEnum);
            tarea.Property(t => t.Cantidad).HasPrecision(8, 2);
            tarea.Ignore(t => t.EsDeAgua);
        });
        builder.Navigation(p => p.Tareas).HasField("_tareas").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.IncluyeAgua);
        builder.Ignore(p => p.RequisitosSalud);
    }
}
