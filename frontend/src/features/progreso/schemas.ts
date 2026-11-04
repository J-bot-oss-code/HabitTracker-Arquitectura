import { z } from 'zod'
import { LIMITES_CORPORALES } from '@/features/publico/types'
import { hoyUtcInput } from '@/shared/lib/fechas'
import { ZONAS, type Zona } from './types'

// Límite propuesto para el backend (mejora futura): peso y medidas aún no existen en
// Host/WebApi. El peso usa los límites corporales del perfil.
export const LIMITES_MEDIDA: Record<Zona, { min: number; max: number }> = {
  Cintura: { min: 40, max: 250 },
  Pecho: { min: 40, max: 250 },
  Brazo: { min: 10, max: 100 },
  Grasa: { min: 3, max: 60 },
}

// Límite propuesto para el backend (mejora futura): evita años absurdos que deforman la escala.
export const FECHA_MINIMA = '2000-01-01'

const fecha = z
  .string()
  .regex(/^\d{4}-\d{2}-\d{2}$/, 'Elige una fecha.')
  .refine((f) => f >= FECHA_MINIMA, 'Elige una fecha a partir del año 2000.')
  .refine((f) => f <= hoyUtcInput(), 'No puedes registrar una fecha futura.')

const numero = (campo: string) => z.number({ error: `Escribe ${campo}.` })

const { pesoKg } = LIMITES_CORPORALES

export const pesoSchema = z.object({
  fecha,
  pesoKg: numero('tu peso')
    .min(pesoKg.min, `Debe ser al menos ${pesoKg.min} kg.`)
    .max(pesoKg.max, `Debe ser como máximo ${pesoKg.max} kg.`),
  // Vacío = no cambiar el objetivo (NaN lo produce un campo numérico sin contenido).
  objetivoKg: z
    .number()
    .min(pesoKg.min, `Debe ser al menos ${pesoKg.min} kg.`)
    .max(pesoKg.max, `Debe ser como máximo ${pesoKg.max} kg.`)
    .or(z.nan())
    .optional(),
})

export const medicionSchema = z
  .object({
    fecha,
    zona: z.enum(ZONAS, { error: 'Elige una zona.' }),
    valor: numero('la medida'),
  })
  .superRefine((d, ctx) => {
    const { min, max } = LIMITES_MEDIDA[d.zona]
    if (Number.isNaN(d.valor) || d.valor < min || d.valor > max) {
      ctx.addIssue({
        code: 'custom',
        path: ['valor'],
        message: `Debe estar entre ${min} y ${max}.`,
      })
    }
  })

export type PesoForm = z.infer<typeof pesoSchema>
export type MedicionForm = z.infer<typeof medicionSchema>
