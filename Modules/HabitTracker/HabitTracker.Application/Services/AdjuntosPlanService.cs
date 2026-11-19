using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Services;

public sealed record AdjuntoPlanDto(Guid DocumentoId, string Nombre, string Tipo, long TamanoBytes, DateTime FechaSubidaUtc, DateTime AdjuntadoEnUtc);

/// <summary>Un archivo y el plan al que está vinculado: «Mis archivos» muestra con esto cuáles piden permiso para eliminarse.</summary>
public sealed record VinculoDocumentoDto(Guid DocumentoId, Guid PlanId, string PlanNombre, bool EnCurso);

public interface IAdjuntosPlanService
{
    Task<IReadOnlyList<AdjuntoPlanDto>> ListarDePlanAsync(Guid usuarioId, Guid planId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VinculoDocumentoDto>> ListarVinculosAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Vincula un archivo propio a un plan propio en curso. <c>plan_no_en_curso</c>, <c>documento_no_encontrado</c>,
    /// <c>adjunto_duplicado</c> y <c>limite_adjuntos</c> (10 por plan).
    /// </summary>
    Task AdjuntarAsync(Guid usuarioId, Guid planId, Guid documentoId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quita el vínculo (el archivo no se elimina). Con el plan en curso exige el mismo permiso que eliminar el archivo; si no, quitar el
    /// vínculo sería la forma de esquivar la regla (<c>documento_requiere_permiso</c>).
    /// </summary>
    Task QuitarAsync(Guid usuarioId, string correo, Guid planId, Guid documentoId, CancellationToken cancellationToken = default);

}

public sealed class AdjuntosPlanService(
    IPlanUsuarioRepository planes,
    IAdjuntoPlanRepository adjuntos,
    IDocumentosUsuario documentos,
    IPermisoService permisos,
    IUnitOfWork unitOfWork,
    TimeProvider? reloj = null) : IAdjuntosPlanService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<IReadOnlyList<AdjuntoPlanDto>> ListarDePlanAsync(Guid usuarioId, Guid planId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        await ObtenerPlanAsync(usuarioId, planId, cancellationToken);

        // Una consulta de archivos para todos los vínculos. Un archivo eliminado ya no aparece (el vínculo viejo se limpia al adjuntar otro).
        var vigentes = (await documentos.ListarAsync(usuarioId, cancellationToken)).ToDictionary(d => d.Id);
        return [.. (await adjuntos.ListarDePlanAsync(planId, usuarioId, cancellationToken))
            .Where(a => vigentes.ContainsKey(a.DocumentoId))
            .Select(a => { var d = vigentes[a.DocumentoId]; return new AdjuntoPlanDto(d.Id, d.Nombre, d.Tipo, d.TamanoBytes, d.FechaSubidaUtc, a.AdjuntadoEnUtc); })];
    }

    public async Task<IReadOnlyList<VinculoDocumentoDto>> ListarVinculosAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        // Un archivo eliminado ya no pide permiso ni aparece: su vínculo viejo se limpia al adjuntar otro.
        var vigentes = (await documentos.ListarAsync(usuarioId, cancellationToken)).Select(d => d.Id).ToHashSet();
        return [.. (await adjuntos.ListarDeUsuarioAsync(usuarioId, cancellationToken))
            .Where(v => vigentes.Contains(v.Adjunto.DocumentoId))
            .Select(v => new VinculoDocumentoDto(
                v.Adjunto.DocumentoId, v.Adjunto.PlanUsuarioId, v.PlanNombre,
                v.Estado is EstadoPlanUsuario.Activo or EstadoPlanUsuario.Pausado))];
    }

    public async Task AdjuntarAsync(Guid usuarioId, Guid planId, Guid documentoId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var plan = await ObtenerPlanAsync(usuarioId, planId, cancellationToken);
        if (!plan.EstaEnCurso)
            throw new ReglaNegocioException(ReglaNegocioException.PlanNoEnCurso, "Solo puedes adjuntar archivos a un plan en curso.");

        // RD-06: el archivo tiene que ser de esta persona; uno ajeno o eliminado se responde igual que uno que no existe.
        if (await documentos.ObtenerAsync(documentoId, usuarioId, cancellationToken) is null)
            throw new RecursoNoEncontradoException(RecursoNoEncontradoException.DocumentoNoEncontrado, "No encontramos ese archivo.");

        // Se limpian los vínculos de archivos que ya se eliminaron: no deben ocupar un lugar de los 10.
        var vivos = 0;
        foreach (var existente in await adjuntos.ListarDePlanAsync(planId, usuarioId, cancellationToken))
        {
            if (await documentos.ObtenerAsync(existente.DocumentoId, usuarioId, cancellationToken) is null)
            {
                await adjuntos.QuitarAsync(existente, cancellationToken);
                continue;
            }
            if (existente.DocumentoId == documentoId)
                throw new ReglaNegocioException(ReglaNegocioException.AdjuntoDuplicado, "Ese archivo ya está en este plan.");
            vivos++;
        }
        if (vivos >= AdjuntoPlan.MaximoPorPlan)
            throw new ReglaNegocioException(ReglaNegocioException.LimiteAdjuntos, $"Un plan admite hasta {AdjuntoPlan.MaximoPorPlan} archivos.");

        await adjuntos.AgregarAsync(new AdjuntoPlan(planId, usuarioId, documentoId, _reloj.GetUtcNow().UtcDateTime), cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictoDeUnicidadException)
        {
            // Dos peticiones a la vez: el índice único (plan, documento) se quedó con la primera.
            throw new ReglaNegocioException(ReglaNegocioException.AdjuntoDuplicado, "Ese archivo ya está en este plan.");
        }
    }

    public async Task QuitarAsync(Guid usuarioId, string correo, Guid planId, Guid documentoId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correo);
        var plan = await ObtenerPlanAsync(usuarioId, planId, cancellationToken);
        var adjunto = await adjuntos.ObtenerAsync(planId, documentoId, usuarioId, cancellationToken)
            ?? throw new RecursoNoEncontradoException(RecursoNoEncontradoException.DocumentoNoEncontrado, "Ese archivo no está en este plan.");

        if (plan.EstaEnCurso && !await permisos.TienePermisoEfectivoAsync(correo, PermisosDocumentos.EliminarVinculado(documentoId), cancellationToken))
            throw new PermisoRequeridoException();

        await adjuntos.QuitarAsync(adjunto, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task<PlanUsuario> ObtenerPlanAsync(Guid usuarioId, Guid planId, CancellationToken cancellationToken) =>
        await planes.ObtenerAsync(planId, usuarioId, cancellationToken)
        ?? throw new RecursoNoEncontradoException(RecursoNoEncontradoException.PlanUsuarioNoEncontrado, "No encontramos este plan.");
}
