using Permissions.Domain.Enums;

namespace Permissions.Domain.Entities;

internal static class TransicionesSolicitudPermiso
{
    public static bool EsValida(EstadoSolicitudPermiso estadoActual, EstadoSolicitudPermiso nuevoEstado) =>
        (estadoActual, nuevoEstado) switch
        {
            (EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Aprobada) => true,
            (EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Pendiente) => true,
            (EstadoSolicitudPermiso.Aprobada, EstadoSolicitudPermiso.Aplicada) => true,
            (EstadoSolicitudPermiso.Pendiente, EstadoSolicitudPermiso.Rechazada) => true,
            _ => false
        };
}
