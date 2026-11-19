// Contrato de AdminColaCorreosController (/api/admin/email-queue), solo Administrador.
// Ver AccessControl.Application/DTOs/ColaCorreosDtos.cs.
export const ESTADOS_CORREO = ['Pendiente', 'Procesando', 'Enviado', 'Fallido'] as const
export type EstadoCorreo = (typeof ESTADOS_CORREO)[number]

// CorreoEnCola.MaxIntentos: un correo Fallido con este número de intentos ya no se reintenta.
export const MAX_INTENTOS_CORREO = 3

// Sin el cuerpo: puede llevar códigos de activación.
export interface CorreoColaDto {
  id: string
  destinatario: string
  asunto: string
  estado: EstadoCorreo
  intentos: number
  fechaCreacion: string
  fechaUltimoIntento: string | null
  fechaEnvio: string | null
  ultimoError: string | null
}

export interface ResultadoColaDto {
  enviados: number
  fallidos: number
  recuperados: number
}
