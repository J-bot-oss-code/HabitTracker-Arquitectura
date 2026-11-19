using Auditing.Application.Interfaces;
using Auditing.Domain;
using Auditing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Auditing.Infrastructure.Repositories;

/// <summary>Solo agrega y consulta (RF-AUD-06): no existe ningún método para editar ni para borrar registros.</summary>
public sealed class RegistroAuditoriaRepository(AuditingDbContext dbContext) : IRegistroAuditoriaRepository
{
    public async Task AgregarAsync(RegistroAuditoria registro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(registro);
        await dbContext.Registros.AddAsync(registro, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginaRegistros> ConsultarAsync(
        FiltroAuditoria filtro, int pagina, int tamano, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // Todo el filtrado, el conteo y la paginación ocurren en la base de datos: nunca se trae el historial a memoria.
        var consulta = dbContext.Registros.AsNoTracking().AsQueryable();
        if (filtro.Usuario is { } usuario)
            consulta = consulta.Where(r => r.Usuario.Contains(usuario));
        if (filtro.Entidad is { } entidad)
            consulta = consulta.Where(r => r.Entidad == entidad);
        if (filtro.EntidadId is { } entidadId)
            consulta = consulta.Where(r => r.EntidadId == entidadId);
        if (filtro.Accion is { } accion)
            consulta = consulta.Where(r => r.Accion == accion);
        if (filtro.DesdeUtc is { } desde)
            consulta = consulta.Where(r => r.FechaUtc >= desde);
        if (filtro.HastaUtcExclusivo is { } hasta)
            consulta = consulta.Where(r => r.FechaUtc < hasta);

        var total = await consulta.CountAsync(cancellationToken);
        var registros = await consulta
            .OrderByDescending(r => r.FechaUtc).ThenByDescending(r => r.Id) // el desempate evita repetir o saltar filas entre páginas
            .Skip((pagina - 1) * tamano).Take(tamano)
            .ToListAsync(cancellationToken);

        return new PaginaRegistros(total, registros);
    }
}
