using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Planes;
using System.Data.Common;
using HabitTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HabitTracker.Infrastructure.Repositories;

public sealed class PlanUsuarioRepository(HabitTrackerDbContext contexto) : IPlanUsuarioRepository
{
    // Siempre por dueña: un plan ajeno ni siquiera se encuentra (RD-06). Las pausas, al ser un hijo propio, vienen con el plan.
    public Task<PlanUsuario?> ObtenerAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default) =>
        contexto.PlanesUsuario.FirstOrDefaultAsync(p => p.Id == planId && p.UsuarioId == usuarioId, cancellationToken);

    public async Task<IReadOnlyList<PlanUsuario>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await contexto.PlanesUsuario
            .Where(p => p.UsuarioId == usuarioId)
            .OrderByDescending(p => p.FechaInicio)
            .ToListAsync(cancellationToken);

    public async Task AgregarAsync(PlanUsuario plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        await contexto.PlanesUsuario.AddAsync(plan, cancellationToken);
    }

    // Las pausas caen en cascada con el plan; los hábitos del plan quedan sueltos (SetNull) y la baja los borra aparte.
    public async Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await contexto.PlanesUsuario.Where(p => p.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);
}

public sealed class LogroUsuarioRepository(HabitTrackerDbContext contexto) : ILogroUsuarioRepository
{
    public async Task<IReadOnlyList<LogroUsuario>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await contexto.LogrosUsuario.Where(l => l.UsuarioId == usuarioId).ToListAsync(cancellationToken);

    public async Task<bool> AgregarSiNoExisteAsync(LogroUsuario logro, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(logro);
        var clave = logro.Clave.ToString();
        try
        {
            // Una sola instrucción: no hay un hueco entre mirar si existe e insertar. Si aun así otra petición se adelanta, el
            // índice único (persona, clave) lo rechaza y aquí se toma como "ya estaba".
            var filas = await contexto.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO LogrosUsuario (Id, UsuarioId, Clave, ObtenidoEn)
                SELECT {logro.Id}, {logro.UsuarioId}, {clave}, {logro.ObtenidoEn}
                WHERE NOT EXISTS (SELECT 1 FROM LogrosUsuario WHERE UsuarioId = {logro.UsuarioId} AND Clave = {clave})",
                cancellationToken);
            return filas > 0;
        }
        catch (DbException ex) when (ViolacionesDeUnicidad.Es(ex))
        {
            return false;
        }
    }

    public async Task<int> EliminarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default) =>
        await contexto.LogrosUsuario.Where(l => l.UsuarioId == usuarioId).ExecuteDeleteAsync(cancellationToken);
}
