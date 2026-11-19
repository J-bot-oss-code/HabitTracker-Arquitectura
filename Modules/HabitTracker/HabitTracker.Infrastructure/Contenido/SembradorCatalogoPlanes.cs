using HabitTracker.Domain.Planes;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Contenido;

public sealed record ResultadoSiembraCatalogo(int Nuevos, int Actualizados, int Retirados);

/// <summary>
/// Lleva el catálogo embebido a la base al arrancar. Upsert por slug: un plan nuevo se agrega, uno con
/// <c>versionContenido</c> mayor se actualiza y uno que ya no está en el JSON se retira (<c>Activo=false</c>). Nunca borra,
/// porque puede haber personas siguiendo ese plan. Pensado para una sola réplica, como el resto de la inicialización.
/// </summary>
public sealed class SembradorCatalogoPlanes(HabitTrackerDbContext contexto)
{
    public Task<ResultadoSiembraCatalogo> SembrarAsync(CancellationToken cancellationToken = default) =>
        SembrarAsync(LectorCatalogoPlanes.Leer(), cancellationToken);

    public async Task<ResultadoSiembraCatalogo> SembrarAsync(IReadOnlyList<ContenidoPlan> catalogo, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(catalogo);
        var existentes = await contexto.PlanesPlantilla.ToDictionaryAsync(p => p.Slug, StringComparer.Ordinal, cancellationToken);
        int nuevos = 0, actualizados = 0, retirados = 0;

        foreach (var contenido in catalogo)
        {
            if (!existentes.TryGetValue(contenido.Slug, out var plan))
            {
                contexto.PlanesPlantilla.Add(new PlanPlantilla(contenido));
                nuevos++;
            }
            else if (contenido.VersionContenido > plan.VersionContenido || !plan.Activo)
            {
                // Una versión igual o menor no pisa lo guardado; un plan retirado que vuelve al JSON se reactiva.
                plan.Actualizar(contenido);
                actualizados++;
            }
        }

        var slugs = catalogo.Select(c => c.Slug).ToHashSet(StringComparer.Ordinal);
        foreach (var plan in existentes.Values.Where(p => p.Activo && !slugs.Contains(p.Slug)))
        {
            plan.Retirar();
            retirados++;
        }

        await contexto.SaveChangesAsync(cancellationToken);
        return new ResultadoSiembraCatalogo(nuevos, actualizados, retirados);
    }
}
