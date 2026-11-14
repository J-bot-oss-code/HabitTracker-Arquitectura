using AccessControl.Domain.Interfaces;
using DocumentManager.Application.Interfaces;
using HabitTracker.Application.Interfaces;
using HabitTracker.Domain.Planes;

namespace WebApi.Adapters;

/// <summary>
/// Compone la regla entre módulos: el dueño elimina libremente su archivo, salvo que esté vinculado a un plan <b>en curso</b>; en ese caso
/// necesita el permiso <c>Documents.DeleteLinked.{id}</c> aplicado por un Administrador. Vive en el Host porque ni el Manejador de
/// documentos ni Permisos conocen a HabitTracker (RD-03). Usa el repositorio y no el servicio de adjuntos para no cerrar un ciclo de
/// inyección (Manejador → guarda → adjuntos → Manejador).
/// </summary>
public sealed class GuardaEliminacionDocumentoAdapter(
    IAdjuntoPlanRepository adjuntos,
    IPermisoService permisos,
    IUsuarioRepository usuarios) : IGuardaEliminacionDocumento
{
    public async Task<bool> PermiteEliminarAsync(Guid documentoId, Guid usuarioId, CancellationToken cancellationToken = default)
    {
        if (!await adjuntos.DocumentoEnPlanEnCursoAsync(documentoId, usuarioId, cancellationToken))
            return true;

        var usuario = await usuarios.GetByIdAsync(usuarioId);
        return usuario is not null
            && await permisos.TienePermisoEfectivoAsync(usuario.Correo.Valor, PermisosDocumentos.EliminarVinculado(documentoId), cancellationToken);
    }
}
