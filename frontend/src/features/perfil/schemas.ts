import { z } from 'zod'
import { METAS_CATALOGO } from '@/features/publico/api'
import { LIMITES_CORPORALES, NIVELES_ACTIVIDAD, SEXOS } from '@/features/publico/types'

// Límite propuesto para el backend (mejora futura): el perfil y el plan aún no existen en
// Host/WebApi. Los corporales son los mismos de la calculadora pública.
export const LIMITES_PLAN = {
  diasPorSemana: { min: 1, max: 7 },
  minutosPorSesion: { min: 10, max: 180 },
} as const

const entero = (campo: string, { min, max }: { min: number; max: number }, unidad: string) =>
  z
    .number({ error: `Escribe ${campo}.` })
    .int(`Escribe ${campo} sin decimales.`)
    .min(min, `Debe ser al menos ${min} ${unidad}.`)
    .max(max, `Debe ser como máximo ${max} ${unidad}.`)

const decimal = (campo: string, { min, max }: { min: number; max: number }, unidad: string) =>
  z
    .number({ error: `Escribe ${campo}.` })
    .min(min, `Debe ser al menos ${min} ${unidad}.`)
    .max(max, `Debe ser como máximo ${max} ${unidad}.`)

// "no-decir" se envía como null: el sexo es opcional (minimización, docs/mejoras §7.2).
export const OPCIONES_SEXO = [...SEXOS, 'no-decir'] as const
export const SIN_META = 'despues'

export const datosBasicosSchema = z.object({
  edad: entero('tu edad', LIMITES_CORPORALES.edad, 'años'),
  sexo: z.enum(OPCIONES_SEXO, { error: 'Elige una opción.' }),
  alturaCm: decimal('tu altura', LIMITES_CORPORALES.alturaCm, 'cm'),
  pesoKg: decimal('tu peso', LIMITES_CORPORALES.pesoKg, 'kg'),
})

export const actividadSchema = z.object({
  actividad: z.enum(NIVELES_ACTIVIDAD, { error: 'Elige tu nivel de actividad.' }),
  diasPorSemana: entero('los días', LIMITES_PLAN.diasPorSemana, 'días'),
  minutosPorSesion: entero('los minutos', LIMITES_PLAN.minutosPorSesion, 'minutos'),
  metaSlug: z.enum([SIN_META, ...METAS_CATALOGO.map((m) => m.slug)], {
    error: 'Elige una meta o "Elegir más tarde".',
  }),
})

// Casilla obligatoria: empieza en false (nada marcado por defecto) y solo vale true.
const aceptado = (mensaje: string) => z.boolean().refine((v) => v, { error: mensaje })

export const MENSAJES_CONSENTIMIENTO = {
  terminos: 'Debes aceptar los Términos de uso y el Aviso de salud.',
  privacidad: 'Debes aceptar la Política de privacidad y el tratamiento de datos de salud.',
  avisoSalud: 'Confirma que leíste el aviso de salud.',
} as const

// Casillas del registro (§5.2): dos obligatorias y una opcional, ninguna marcada por defecto.
export const consentimientoSchema = z.object({
  terminos: aceptado(MENSAJES_CONSENTIMIENTO.terminos),
  privacidad: aceptado(MENSAJES_CONSENTIMIENTO.privacidad),
  marketing: z.boolean(),
})

export const avisoSaludSchema = z.object({
  avisoSalud: aceptado(MENSAJES_CONSENTIMIENTO.avisoSalud),
})

export type DatosBasicosForm = z.infer<typeof datosBasicosSchema>
export type ActividadForm = z.infer<typeof actividadSchema>
export type ConsentimientoForm = z.infer<typeof consentimientoSchema>

// Onboarding (pasos 3 a 5 del alta). Un solo formulario; cada paso valida sus campos. Si el
// registro se hizo en otro dispositivo, aquí se vuelven a pedir los consentimientos.
export function onboardingSchema(pedirConsentimiento: boolean) {
  return z
    .object({
      ...datosBasicosSchema.shape,
      ...actividadSchema.shape,
      ...avisoSaludSchema.shape,
      terminos: z.boolean(),
      privacidad: z.boolean(),
      marketing: z.boolean(),
    })
    .superRefine((d, ctx) => {
      if (!pedirConsentimiento) return
      if (!d.terminos) {
        ctx.addIssue({
          code: 'custom',
          path: ['terminos'],
          message: MENSAJES_CONSENTIMIENTO.terminos,
        })
      }
      if (!d.privacidad) {
        ctx.addIssue({
          code: 'custom',
          path: ['privacidad'],
          message: MENSAJES_CONSENTIMIENTO.privacidad,
        })
      }
    })
}

export type OnboardingForm = z.infer<ReturnType<typeof onboardingSchema>>

// Campos de cada paso, en orden (para validar paso a paso con trigger).
export const CAMPOS_POR_PASO = [
  ['edad', 'sexo', 'alturaCm', 'pesoKg'],
  ['actividad', 'diasPorSemana', 'minutosPorSesion', 'metaSlug'],
  ['avisoSalud', 'terminos', 'privacidad', 'marketing'],
] as const satisfies readonly (readonly (keyof OnboardingForm)[])[]
