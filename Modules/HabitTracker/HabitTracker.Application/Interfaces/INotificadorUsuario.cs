using HabitTracker.Domain.Adherencia;

namespace HabitTracker.Application.Interfaces;

/// <summary>
/// Punto de extensión: avisa a la persona de algo que pasó en su seguimiento (RF-NEG-07 y RF-NOT-03). HabitTracker no sabe cómo
/// ni por qué canal le llega: lo implementa un adaptador del Host (RD-03). Es opcional: sin él, el módulo funciona igual.
/// Nunca recibe datos de salud: solo la clave del logro (P4).
/// </summary>
public interface INotificadorUsuario
{
    /// <summary>La persona acaba de ganar un logro de constancia (se avisa una sola vez por logro).</summary>
    Task LogroObtenidoAsync(Guid usuarioId, ClaveLogro clave, CancellationToken cancellationToken = default);
}
