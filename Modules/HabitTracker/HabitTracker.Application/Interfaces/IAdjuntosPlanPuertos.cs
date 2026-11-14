using HabitTracker.Domain.Planes;

namespace HabitTracker.Application.Interfaces;

/// <summary>Lo que HabitTracker sabe de un archivo de la persona. Nunca la ruta ni el nombre almacenado.</summary>
public sealed record DocumentoResumen(Guid Id, string Nombre, string Tipo, long TamanoBytes, DateTime FechaSubidaUtc);

/// <summary>Los archivos de una persona, vistos desde HabitTracker. Lo implementa un adaptador del Host sobre el Manejador de documentos (RD-03).</summary>
public interface IDocumentosUsuario
{
    /// <summary>El archivo, o <c>null</c> si no existe, fue eliminado o no es de esa persona.</summary>
    Task<DocumentoResumen?> ObtenerAsync(Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default);
}

public sealed record AdjuntoConPlan(AdjuntoPlan Adjunto, string PlanNombre, EstadoPlanUsuario Estado);

public interface IAdjuntoPlanRepository
{
    /// <summary>Los vínculos de un plan de esa persona, del más antiguo al más reciente.</summary>
    Task<IReadOnlyList<AdjuntoPlan>> ListarDePlanAsync(Guid planId, Guid usuarioId, CancellationToken cancellationToken = default);

    /// <summary>Todos los vínculos de la persona, con el nombre y el estado de su plan.</summary>
    Task<IReadOnlyList<AdjuntoConPlan>> ListarDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);

    Task<AdjuntoPlan?> ObtenerAsync(Guid planId, Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default);

    Task AgregarAsync(AdjuntoPlan adjunto, CancellationToken cancellationToken = default);

    Task QuitarAsync(AdjuntoPlan adjunto, CancellationToken cancellationToken = default);

    /// <summary>El archivo está vinculado a un plan de esa persona que sigue en curso (Activo o En pausa).</summary>
    Task<bool> DocumentoEnPlanEnCursoAsync(Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default);
}
