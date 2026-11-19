import { z } from 'zod'

// Mismas reglas que CrearPermisoRequest: la clave es un nombre con puntos (Habits.AdvancedReconfig), de 3 a 256
// caracteres. Motivo del rechazo ≤ 500 (Permissions.Domain).
export const CLAVE_PERMISO = /^[A-Za-z][A-Za-z0-9]*(\.[A-Za-z0-9]+)+$/

export const crearSolicitudSchema = z.object({
  permisoSolicitado: z
    .string()
    .trim()
    .min(1, 'Elige el permiso que necesitas.')
    .min(3, 'El nombre del permiso es demasiado corto.')
    .max(256, 'El nombre del permiso es demasiado largo.')
    .regex(CLAVE_PERMISO, 'El permiso no es válido. Usa un nombre como Habits.AdvancedReconfig.'),
})

export const rechazarSchema = z.object({
  motivoRechazo: z
    .string()
    .trim()
    .min(1, 'Indica el motivo del rechazo.')
    .max(500, 'El motivo admite como máximo 500 caracteres.'),
})

export type CrearSolicitudForm = z.infer<typeof crearSolicitudSchema>
export type RechazarForm = z.infer<typeof rechazarSchema>
