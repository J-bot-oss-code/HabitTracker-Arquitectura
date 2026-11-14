namespace DocumentManager.Application.Interfaces;

/// <summary>
/// Punto de extensión: quien compone la aplicación puede vetar que el <b>dueño</b> elimine un documento (por ejemplo, porque otro
/// módulo lo usa y pide un permiso). El Manejador de documentos no sabe por qué: solo pregunta. Sin una implementación registrada,
/// todo se permite y el comportamiento es el de siempre (RF-DOC-04). El Administrador no pasa por aquí.
/// </summary>
public interface IGuardaEliminacionDocumento
{
    Task<bool> PermiteEliminarAsync(Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default);
}
