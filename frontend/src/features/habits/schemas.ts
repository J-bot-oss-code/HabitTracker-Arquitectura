import { z } from 'zod'
import { hoyUtcInput } from '@/shared/lib/fechas'
import { PERIODICIDADES } from './types'

// Mismos rangos que los DTOs de HabitsController (RD-07: el servidor valida de nuevo).
const cantidad = (campo: string) =>
  z
    .number({ error: `${campo} debe ser un número.` })
    .min(0.01, `${campo} debe ser mayor que cero.`)
    .max(1_000_000, `${campo} es demasiado grande.`)

const unidad = z
  .string()
  .trim()
  .min(1, 'La unidad es obligatoria.')
  .max(50, 'La unidad admite como máximo 50 caracteres.')

const periodicidad = z.enum(PERIODICIDADES, { error: 'Elige una periodicidad.' })

export const crearHabitoSchema = z.object({
  nombre: z
    .string()
    .trim()
    .min(1, 'El nombre es obligatorio.')
    .max(200, 'El nombre admite como máximo 200 caracteres.'),
  periodicidad,
  cantidadObjetivo: cantidad('La cantidad objetivo'),
  unidad,
  metaId: z.string().trim().pipe(z.guid('El identificador de meta no es válido.')),
})

export const registrarAvanceSchema = z.object({
  cantidad: cantidad('La cantidad'),
  fecha: z
    .string()
    .regex(/^\d{4}-\d{2}-\d{2}$/, 'Elige una fecha.')
    // El backend aún acepta fechas futuras (deuda técnica §6.3); la interfaz no las ofrece.
    .refine((f) => f <= hoyUtcInput(), 'No puedes registrar avances en fechas futuras.'),
})

export const reconfigurarSchema = z.object({
  periodicidad,
  cantidadObjetivo: cantidad('La cantidad objetivo'),
  unidad,
})

export type CrearHabitoForm = z.infer<typeof crearHabitoSchema>
export type RegistrarAvanceForm = z.infer<typeof registrarAvanceSchema>
export type ReconfigurarForm = z.infer<typeof reconfigurarSchema>
