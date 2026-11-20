using AccessControl.Domain.Interfaces;
using Auditing.Application.Interfaces;
using DocumentManager.Application.Interfaces;

namespace WebApi.Adapters;

/// <summary>
/// Lleva al historial de auditoría cada subida y eliminación de documento (RF-AUD-03). Código de composición del Host: el Manejador
/// de documentos y Auditoría no se conocen (RD-03). El Manejador solo conoce el identificador de quien actuó; el historial guarda
/// también su correo, que se busca en Control de Acceso (si la cuenta ya no existe, se usa el identificador). Un fallo al guardar
/// no se traga.
/// </summary>
public sealed class AuditorDocumentosAdapter(IRegistroAuditoriaService historial, IUsuarioRepository usuarios) : IAuditorDocumentos
{
    public async Task RegistrarAsync(
        Guid usuarioId, string accion, Guid documentoId, string valorAnterior, string valorNuevo, CancellationToken cancellationToken = default)
    {
        var actor = await usuarios.GetByIdAsync(usuarioId);
        await historial.RegistrarAsync(
            usuarioId, actor?.Correo.Valor ?? usuarioId.ToString(), accion, "Documento", documentoId.ToString(), valorAnterior, valorNuevo,
            cancellationToken);
    }
}
