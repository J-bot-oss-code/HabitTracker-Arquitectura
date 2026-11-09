import { z } from 'zod'
import { LIMITES_CORPORALES } from '@/features/publico/types'
import {
  ACTIVIDADES_PERFIL,
  ALERGIAS,
  CONDICIONES,
  EXPERIENCIAS,
  LUGARES,
  MAXIMO_OBJETIVOS,
  OBJETIVOS,
  PERFILES_LABORALES,
  SEXOS_PERFIL,
  ZONAS_CORPORALES,
} from './types'

// Mismos límites que PerfilUsuario en el backend (HabitTracker.Domain/Perfil).
export const LIMITES_PLAN = {
  diasPorSemana: { min: 1, max: 7 },
  minutosPorSesion: { min: 10, max: 180 },
} as const

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

export type ConsentimientoForm = z.infer<typeof consentimientoSchema>

const perfilLaboral = z.enum(PERFILES_LABORALES, { error: 'Elige cómo es tu jornada.' })
const zonaHoraria = z
  .string()
  .trim()
  .min(1, 'Elige tu zona horaria.')
  .max(64, 'La zona horaria no es válida.')

// Onboarding de 3 toques (P1): jornada → zona horaria → aviso de salud. Sin datos de salud.
export function onboardingSchema(pedirConsentimiento: boolean) {
  return z
    .object({
      perfilLaboral,
      zonaHoraria,
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
  ['perfilLaboral'],
  ['zonaHoraria'],
  ['avisoSalud', 'terminos', 'privacidad', 'marketing'],
] as const satisfies readonly (readonly (keyof OnboardingForm)[])[]

// --- Configuración › Perfil y salud ---

// Un campo numérico vacío llega como NaN (valueAsNumber): es "sin dato" (null), no un error.
const numeroOpcional = (
  campo: string,
  { min, max }: { min: number; max: number },
  entero = false,
) =>
  z.preprocess(
    (v) => (typeof v === 'number' && Number.isNaN(v) ? null : v),
    z
      .number({ error: `Escribe ${campo}.` })
      .min(min, `Debe ser al menos ${min}.`)
      .max(max, `Debe ser como máximo ${max}.`)
      .refine((n) => !entero || Number.isInteger(n), `Escribe ${campo} sin decimales.`)
      .nullable(),
  )

// "" (sin elegir) se envía como null.
const opcional = <T extends readonly [string, ...string[]]>(valores: T) =>
  z.preprocess((v) => (v === '' ? null : v), z.enum(valores).nullable())

const hora = z.preprocess(
  (v) => (v === '' ? null : v),
  z
    .string()
    .regex(/^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$/, 'Escribe la hora como HH:mm.')
    .nullable(),
)

export const perfilSchema = z.object({
  perfilLaboral,
  zonaHoraria,
  horaEntradaTrabajo: hora,
  horaSalidaTrabajo: hora,
  horaDespertar: hora,
  horaDormir: hora,
  edad: numeroOpcional('tu edad', LIMITES_CORPORALES.edad, true),
  sexo: opcional(SEXOS_PERFIL),
  alturaCm: numeroOpcional('tu altura', LIMITES_CORPORALES.alturaCm),
  pesoKg: numeroOpcional('tu peso', LIMITES_CORPORALES.pesoKg),
  actividad: opcional(ACTIVIDADES_PERFIL),
  diasPorSemana: numeroOpcional('los días', LIMITES_PLAN.diasPorSemana, true),
  minutosPorSesion: numeroOpcional('los minutos', LIMITES_PLAN.minutosPorSesion, true),
})

export type PerfilForm = z.input<typeof perfilSchema>
export type PerfilValidado = z.output<typeof perfilSchema>

export const objetivosSchema = z
  .object({
    objetivos: z
      .array(z.enum(OBJETIVOS))
      .min(1, 'Elige al menos un objetivo.')
      .max(MAXIMO_OBJETIVOS, `Elige como máximo ${MAXIMO_OBJETIVOS} objetivos.`),
    zonas: z.array(z.enum(ZONAS_CORPORALES)),
    experiencia: z.enum(EXPERIENCIAS, { error: 'Elige tu experiencia.' }),
    lugar: z.enum(LUGARES, { error: 'Elige dónde entrenarías.' }),
  })
  // Igual que el dominio: las zonas solo aplican a "definir o tonificar".
  .transform((d) => (d.objetivos.includes('DefinirTonificar') ? d : { ...d, zonas: [] }))

// "Ninguna" es exclusiva: no se combina con otras opciones (mismo criterio que el backend).
const listaConNinguna = <T extends readonly ['Ninguna', ...string[]]>(valores: T, campo: string) =>
  z
    .array(z.enum(valores))
    .min(1, `Marca al menos una opción en ${campo} (o "Ninguna").`)
    .refine(
      (l) => !l.includes('Ninguna') || l.length === 1,
      '"Ninguna" no se combina con otras opciones.',
    )

export const condicionesSchema = z.object({
  condiciones: listaConNinguna(CONDICIONES, 'condiciones'),
  alergias: listaConNinguna(ALERGIAS, 'alergias'),
})

export type ObjetivosForm = z.input<typeof objetivosSchema>
export type CondicionesForm = z.input<typeof condicionesSchema>
