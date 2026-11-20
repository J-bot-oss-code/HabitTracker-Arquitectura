namespace DocumentManager.Application.Interfaces;

/// <summary>
/// Punto de extensión: deja constancia en el historial de auditoría de cada subida y de cada eliminación de documento
/// (RF-AUD-03, RF-DOC-06). El Manejador de documentos no sabe dónde se guarda: lo implementa un adaptador del Host (RD-03). Es
/// opcional: sin él, el módulo funciona igual. Un fallo al registrar NO se traga: la operación responde error.
/// </summary>
public interface IAuditorDocumentos
{
    /// <summary>La persona <paramref name="usuarioId"/> hizo algo con el documento: el valor anterior y el nuevo lo describen.</summary>
    Task RegistrarAsync(
        Guid usuarioId, string accion, Guid documentoId, string valorAnterior, string valorNuevo, CancellationToken cancellationToken = default);
}
