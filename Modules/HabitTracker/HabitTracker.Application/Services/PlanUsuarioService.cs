using HabitTracker.Application.DTOs;
using HabitTracker.Application.Exceptions;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Adherencia;
using HabitTracker.Domain.Entities;
using HabitTracker.Domain.Excepciones;
using HabitTracker.Domain.Perfil;
using HabitTracker.Domain.Planes;
using HabitTracker.Domain.Salud;

namespace HabitTracker.Application.Services;

public interface IPlanUsuarioService
{
    /// <summary>
    /// Empieza un plan del catálogo: crea sus hábitos, ya activos. Rechaza con <c>plan_no_apto</c> (400), exige el
    /// consentimiento de salud solo si el plan pide el cuestionario (409) y limita a 3 planes en curso sin repetir plantilla.
    /// </summary>
    Task<PlanUsuarioDto> InscribirseAsync(Guid usuarioId, InscribirseCommand command, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlanUsuarioDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Un plan propio; 404 <c>plan_usuario_no_encontrado</c> también si es de otra persona (no se revela que existe).</summary>
    Task<PlanUsuarioDto> ObtenerAsync(Guid usuarioId, Guid planId, CancellationToken cancellationToken = default);

    Task<PlanUsuarioDto> CambiarEstadoAsync(Guid usuarioId, Guid planId, CambiarEstadoPlanCommand command, CancellationToken cancellationToken = default);

    /// <summary>Aplica un ajuste que la persona eligió (modo suave o extender). Nunca se aplica solo.</summary>
    Task<PlanUsuarioDto> AjustarAsync(Guid usuarioId, Guid planId, AjustarPlanCommand command, CancellationToken cancellationToken = default);

    Task<PlanUsuarioDto> ConfigurarRecordatorioAsync(Guid usuarioId, Guid planId, ConfigurarRecordatorioCommand command, CancellationToken cancellationToken = default);
}

public sealed class PlanUsuarioService(
    IPlanUsuarioRepository planes,
    IPlanPlantillaRepository plantillas,
    IHabitoRepository habitos,
    IPerfilUsuarioRepository perfiles,
    ICuestionarioSaludRepository cuestionarios,
    IConsentimientoSalud consentimiento,
    IUnitOfWork unitOfWork,
    TimeProvider? reloj = null) : IPlanUsuarioService
{
    private readonly TimeProvider _reloj = reloj ?? TimeProvider.System;

    public async Task<PlanUsuarioDto> InscribirseAsync(Guid usuarioId, InscribirseCommand command, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        ArgumentNullException.ThrowIfNull(command);
        if (command.Ancla is { } ancla && !Enum.IsDefined(ancla))
            throw new ArgumentException("El momento del día no es válido.", nameof(command));

        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken)
            ?? throw new RecursoNoEncontradoException(RecursoNoEncontradoException.PerfilNoEncontrado, "Aún no completaste tu perfil.");

        var plantilla = EsSlugValido(command.Slug) ? await plantillas.ObtenerActivoPorSlugAsync(command.Slug, cancellationToken) : null;
        if (plantilla is null)
            throw new RecursoNoEncontradoException(RecursoNoEncontradoException.PlanNoEncontrado, "No encontramos ese plan.");

        // P1: un Básico sin requisitos de salud no pide el consentimiento ni el cuestionario.
        var consiente = await consentimiento.TieneConsentimientoDatosSaludAsync(usuarioId, cancellationToken);
        if (plantilla.RequiereCuestionario && !consiente)
            throw new ConsentimientoSaludRequeridoException();

        var cuestionario = consiente ? await cuestionarios.ObtenerAsync(usuarioId, cancellationToken) : null;
        if (EvaluadorSalud.BloquesFaltantesPara(plantilla.RequisitosSalud, cuestionario).Count > 0)
            throw new ReglaNegocioException(ReglaNegocioException.CuestionarioIncompleto,
                "Faltan respuestas de tu cuestionario de salud para empezar este plan.");

        // Única definición de "no apto": la misma que usa el recomendador.
        var evaluacion = ReglasSeguridadPlanes.Evaluar(plantilla, SituacionPersona.Crear(perfil, cuestionario, consiente));
        if (!evaluacion.EsApto)
            throw new ReglaNegocioException(ReglaNegocioException.PlanNoApto, evaluacion.Razones[0].Mensaje);

        var enCurso = (await planes.ListarDeUsuarioAsync(usuarioId, cancellationToken)).Where(p => p.EstaEnCurso).ToList();
        if (enCurso.Any(p => p.PlanPlantillaId == plantilla.Id))
            throw new ReglaNegocioException(ReglaNegocioException.PlanYaActivo, "Ya estás siguiendo este plan.");
        if (enCurso.Count >= PlanUsuario.MaximoPlanesEnCurso)
            throw new ReglaNegocioException(ReglaNegocioException.LimitePlanes,
                $"Puedes seguir {PlanUsuario.MaximoPlanesEnCurso} planes a la vez. Termina o cancela uno para empezar otro.");

        var ahora = Guardas.Ahora(_reloj);
        var hora = command.Hora ?? ResolvedorAncla.Resolver(command.Ancla ?? plantilla.Tareas[0].AnclaSugerida, JornadaPersona.Desde(perfil));
        var plan = PlanUsuario.Iniciar(usuarioId, plantilla, hora, ahora);
        var nuevos = plantilla.Tareas.Select(t => Habito.DesdeTareaDePlan(usuarioId, plan.Id, t)).ToList();

        await planes.AgregarAsync(plan, cancellationToken);
        foreach (var habito in nuevos)
            await habitos.AddAsync(habito, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConflictoDeUnicidadException)
        {
            // Dos peticiones a la vez para el mismo plan: el índice único dejó pasar a una sola.
            throw new ReglaNegocioException(ReglaNegocioException.PlanYaActivo, "Ya estás siguiendo este plan.");
        }

        return Seguimiento.ADto(plan, nuevos, plantilla, perfil, ahora);
    }

    public async Task<IReadOnlyList<PlanUsuarioDto>> ListarAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var lista = await planes.ListarDeUsuarioAsync(usuarioId, cancellationToken);
        if (lista.Count == 0)
            return [];

        var perfil = await perfiles.ObtenerAsync(usuarioId, cancellationToken);
        var todos = await habitos.GetByUsuarioIdSoloLecturaAsync(usuarioId, cancellationToken);
        var slugs = lista.Where(p => p.Slug is not null).Select(p => p.Slug!).Distinct().ToList();
        var porSlug = (await plantillas.ObtenerPorSlugsAsync(slugs, cancellationToken)).ToDictionary(p => p.Slug, StringComparer.Ordinal);
        var ahora = Guardas.Ahora(_reloj);

        return [.. lista.Select(p => Seguimiento.ADto(
            p, Seguimiento.HabitosDe(p, todos), p.Slug is not null && porSlug.TryGetValue(p.Slug, out var t) ? t : null, perfil, ahora))];
    }

    public async Task<PlanUsuarioDto> ObtenerAsync(Guid usuarioId, Guid planId, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        var plan = await ObtenerPropioAsync(usuarioId, planId, cancellationToken);
        return await ADtoAsync(plan, cancellationToken);
    }

    public async Task<PlanUsuarioDto> CambiarEstadoAsync(Guid usuarioId, Guid planId, CambiarEstadoPlanCommand command, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        ArgumentNullException.ThrowIfNull(command);
        var plan = await ObtenerPropioAsync(usuarioId, planId, cancellationToken);
        var ahora = Guardas.Ahora(_reloj);
        var delPlan = await habitos.GetByPlanUsuarioIdAsync(usuarioId, plan.Id, cancellationToken);

        // Primero la transición del plan (es la que puede rechazar); los hábitos siguen solo si fue válida.
        switch (command.NuevoEstado)
        {
            case EstadoPlanUsuario.Pausado:
                if (command.MotivoPausa is not { } motivo)
                    throw new ArgumentException("Elige el motivo de la pausa.", nameof(command));
                plan.Pausar(motivo, ahora);
                Para(delPlan, EstadoHabito.Activo, h => h.PausarPorPlan(usuarioId));
                break;
            case EstadoPlanUsuario.Activo:
                plan.Reanudar(ahora, Seguimiento.Zona(await perfiles.ObtenerAsync(usuarioId, cancellationToken)));
                // Solo vuelven los que el plan pausó: uno que la persona dejó en pausa a propósito sigue como lo dejó.
                foreach (var habito in delPlan)
                    habito.ReanudarPorPlan(usuarioId);
                break;
            case EstadoPlanUsuario.Completado:
                plan.Completar(ahora);
                PausarSiNoSeConservan(delPlan, usuarioId, command.ConservarHabitos);
                break;
            case EstadoPlanUsuario.Cancelado:
                plan.Cancelar(ahora);
                PausarSiNoSeConservan(delPlan, usuarioId, command.ConservarHabitos);
                break;
            default:
                throw new ArgumentException("El estado no es válido.", nameof(command));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ADtoAsync(plan, cancellationToken);
    }

    public async Task<PlanUsuarioDto> AjustarAsync(Guid usuarioId, Guid planId, AjustarPlanCommand command, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        ArgumentNullException.ThrowIfNull(command);
        var plan = await ObtenerPropioAsync(usuarioId, planId, cancellationToken);

        switch (command.Tipo)
        {
            case TipoAjuste.ModoSuave:
                plan.ActivarModoSuave(Guardas.Ahora(_reloj));
                break;
            case TipoAjuste.Extender:
                plan.Extender();
                break;
            default:
                // Pausar es una transición (con su motivo): va por CambiarEstadoAsync.
                throw new ArgumentException("Ese ajuste no existe.", nameof(command));
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ADtoAsync(plan, cancellationToken);
    }

    public async Task<PlanUsuarioDto> ConfigurarRecordatorioAsync(Guid usuarioId, Guid planId, ConfigurarRecordatorioCommand command, CancellationToken cancellationToken = default)
    {
        Guardas.ExigirIdentidad(usuarioId);
        ArgumentNullException.ThrowIfNull(command);
        var plan = await ObtenerPropioAsync(usuarioId, planId, cancellationToken);

        plan.ConfigurarRecordatorio(command.Hora, command.RecordatoriosActivos);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await ADtoAsync(plan, cancellationToken);
    }

    private async Task<PlanUsuario> ObtenerPropioAsync(Guid usuarioId, Guid planId, CancellationToken cancellationToken) =>
        await planes.ObtenerAsync(planId, usuarioId, cancellationToken)
        ?? throw new RecursoNoEncontradoException(RecursoNoEncontradoException.PlanUsuarioNoEncontrado, "No encontramos ese plan.");

    private async Task<PlanUsuarioDto> ADtoAsync(PlanUsuario plan, CancellationToken cancellationToken)
    {
        var perfil = await perfiles.ObtenerAsync(plan.UsuarioId, cancellationToken);
        var delPlan = await habitos.GetByPlanUsuarioIdAsync(plan.UsuarioId, plan.Id, cancellationToken);
        var plantilla = plan.Slug is null ? null : (await plantillas.ObtenerPorSlugsAsync([plan.Slug], cancellationToken)).FirstOrDefault();
        return Seguimiento.ADto(plan, delPlan, plantilla, perfil, Guardas.Ahora(_reloj));
    }

    private static void Para(IReadOnlyList<Habito> habitos, EstadoHabito estado, Action<Habito> accion)
    {
        foreach (var habito in habitos.Where(h => h.Estado == estado))
            accion(habito);
    }

    // Al terminar el plan no se borra nada. Si la persona conserva los hábitos, siguen activos como hábitos de siempre y
    // mantienen su vínculo con el plan (el historial del plan y su récord no se pierden); si no, quedan en pausa.
    private static void PausarSiNoSeConservan(IReadOnlyList<Habito> habitos, Guid usuarioId, bool conservar)
    {
        if (conservar)
            return;
        Para(habitos, EstadoHabito.Activo, h => h.PausarPorPlan(usuarioId));
    }

    private static bool EsSlugValido(string? slug) =>
        !string.IsNullOrWhiteSpace(slug) && slug.Length <= PlanUsuario.LongitudSlug;
}
