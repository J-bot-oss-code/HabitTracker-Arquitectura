// Contrato de PermissionsController (/api/permissions). Ver Permissions.Application/DTOs.
export const ESTADOS_SOLICITUD = ['Pendiente', 'Aprobada', 'Aplicada', 'Rechazada'] as const
export type EstadoSolicitudPermiso = (typeof ESTADOS_SOLICITUD)[number]

export interface SolicitudPermisoDto {
  id: string
  solicitante: string
  permisoSolicitado: string
  estado: EstadoSolicitudPermiso
  aprobador: string | null
  fechaCreacion: string
  fechaResolucion: string | null
  motivoRechazo: string | null
}

export interface CrearSolicitudRequest {
  permisoSolicitado: string
}

export interface RechazarSolicitudRequest {
  motivoRechazo: string
}

export interface PermisoEfectivoResponse {
  permiso: string
  efectivo: boolean
}
