using HabitTracker.Application.DTOs;
using HabitTracker.Domain.Entities;

namespace HabitTracker.Application.Interfaces;

/// <summary>
/// Casos de uso de los hábitos del usuario autenticado (RF-NEG-02). La identidad llega siempre del token, nunca del
/// cuerpo de la petición. Un hábito ajeno se rechaza con <see cref="UnauthorizedAccessException"/> (403) y uno que no
/// existe con <c>RecursoNoEncontradoException</c> (404, código <c>habito_no_encontrado</c>).
/// </summary>
public interface IHabitoService
{
    /// <summary>Crea un hábito en estado Planificado. La meta es opcional; si viene, debe existir.</summary>
    Task<Guid> CrearHabitoAsync(CrearHabitoCommand command, Guid usuarioActualId, CancellationToken cancellationToken = default);

    /// <summary>Todos los hábitos del usuario con sus registros de avance.</summary>
    Task<IReadOnlyList<HabitoDto>> ConsultarHabitosAsync(Guid usuarioActualId, CancellationToken cancellationToken = default);

    /// <summary>Un hábito del usuario con sus registros de avance.</summary>
    Task<HabitoDto> ObtenerHabitoAsync(Guid habitoId, Guid usuarioActualId, CancellationToken cancellationToken = default);

    /// <summary>Aplica una transición de la máquina de estados (las reglas viven en <c>TransicionesHabito</c>).</summary>
    Task CambiarEstadoAsync(Guid habitoId, EstadoHabito nuevoEstado, Guid usuarioActualId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra avance en un hábito Activo. Rechaza fechas futuras (<c>fecha_futura</c>). Con <paramref name="esVersionMinima"/>
    /// ("Hoy solo lo mínimo") el hábito debe ser de un plan (<c>version_minima_no_disponible</c>).
    /// </summary>
    Task RegistrarAvanceAsync(Guid habitoId, decimal cantidad, DateTime fechaUtc, Guid usuarioActualId,
        bool esVersionMinima = false, CancellationToken cancellationToken = default);

    /// <summary>Reconfigura la meta del hábito; exige el permiso aplicado <c>Habits.AdvancedReconfig</c>.</summary>
    Task ReconfigurarHabitoAsync(Guid habitoId, Periodicidad periodicidad, decimal cantidadObjetivo, string unidad,
        Guid usuarioActualId, string correoSolicitante, CancellationToken cancellationToken = default);

    /// <summary>Borra definitivamente los hábitos del usuario y sus registros (baja de cuenta). Idempotente.</summary>
    Task<int> EliminarDatosDeUsuarioAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
