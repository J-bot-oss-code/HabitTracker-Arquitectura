import { z } from 'zod'
import { LONGITUD_MAXIMA, LONGITUD_MINIMA, TIPOS_SUGERENCIA } from './types'

// Los mismos límites que Sugerencia (dominio): entre 10 y 2000 caracteres DESPUÉS de recortar los espacios.
export const enviarSugerenciaSchema = z.object({
  tipo: z.enum(TIPOS_SUGERENCIA, { message: 'Elige si es una queja o una propuesta de mejora.' }),
  texto: z
    .string()
    .trim()
    .min(LONGITUD_MINIMA, `Cuéntanos con al menos ${LONGITUD_MINIMA} caracteres.`)
    .max(LONGITUD_MAXIMA, `Cuéntanos con ${LONGITUD_MAXIMA} caracteres como máximo.`),
})

export type EnviarSugerenciaForm = z.infer<typeof enviarSugerenciaSchema>
