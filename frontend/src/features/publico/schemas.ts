import { z } from 'zod'
import { LIMITES_CORPORALES, NIVELES_ACTIVIDAD, SEXOS } from './types'

// La calculadora no envía nada al servidor; los límites evitan resultados absurdos y son los
// mismos que usará el perfil del onboarding (LIMITES_CORPORALES).
const enRango = (campo: string, { min, max }: { min: number; max: number }, unidad = '') =>
  z
    .number({ error: `Escribe ${campo}.` })
    .min(min, `${campo[0].toUpperCase()}${campo.slice(1)} debe ser al menos ${min}${unidad}.`)
    .max(max, `${campo[0].toUpperCase()}${campo.slice(1)} debe ser como máximo ${max}${unidad}.`)

export const calculadoraSchema = z.object({
  edad: enRango('la edad', LIMITES_CORPORALES.edad, ' años').int(
    'La edad debe ser un número entero.',
  ),
  sexo: z.enum(SEXOS, { error: 'Elige una opción.' }),
  alturaCm: enRango('la altura', LIMITES_CORPORALES.alturaCm, ' cm'),
  pesoKg: enRango('el peso', LIMITES_CORPORALES.pesoKg, ' kg'),
  actividad: z.enum(NIVELES_ACTIVIDAD, { error: 'Elige tu nivel de actividad.' }),
})

export type CalculadoraForm = z.infer<typeof calculadoraSchema>
