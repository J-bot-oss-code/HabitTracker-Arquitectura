// Contrato de NotificationsController (/api/notifications). Ver Notifications.Application/DTOs.
export const TIPOS_NOTIFICACION = ['SolicitudCreada', 'SolicitudResuelta', 'LogroObtenido'] as const
export type TipoNotificacion = (typeof TIPOS_NOTIFICACION)[number]

export interface NotificacionDto {
  id: string
  tipo: TipoNotificacion
  titulo: string
  mensaje: string
  creadaEnUtc: string
  leida: boolean
  leidaEnUtc: string | null
}

export interface PreferenciasNotificacionesDto {
  correoActivo: boolean
}

// `correoActivo` es obligatorio: sin él el servidor responde 400 (no apaga el correo en silencio).
export interface GuardarPreferenciasRequest {
  correoActivo: boolean
}
