import { z } from 'zod'

// Límites de SolicitudPermiso (Permissions.Domain): permiso ≤ 256, motivo ≤ 500.
export const crearSolicitudSchema = z.object({
  permisoSolicitado: z
    .string()
    .trim()
    .min(1, 'Elige el permiso que necesitas.')
    .max(256, 'El nombre del permiso es demasiado largo.'),
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
