import { z } from 'zod'
import { ANCLAS, esSlugPlan } from '@/features/planes/types'
import {
  MOTIVOS_PAUSA,
  type CambiarEstadoPlanRequest,
  type InscribirseRequest,
  type RecordatorioPlanRequest,
} from './types'

// Mismas reglas que MyPlansController y PlanUsuario (RD-07: el servidor valida de nuevo).

// Un <input type="time"> entrega "HH:mm", y vacío cuando no se eligió nada.
const horaOpcional = z.preprocess(
  (valor) => (valor === '' || valor === null ? undefined : valor),
  z
    .string()
    .regex(/^([01]\d|2[0-3]):[0-5]\d$/, 'Escribe la hora como HH:mm.')
    .optional(),
)

// El backend recibe un TimeOnly ("HH:mm:ss") y devuelve la hora del recordatorio ya como "HH:mm".
export const horaParaApi = (hora: string): string => `${hora}:00`

// POST /api/my-plans: el momento del día (ancla) o una hora concreta son opcionales; si van las dos,
// manda la hora.
export const inscribirseSchema = z.object({
  slug: z.string().refine(esSlugPlan, 'El plan no es válido.'),
  ancla: z.preprocess(
    (valor) => (valor === '' || valor === null ? undefined : valor),
    z.enum(ANCLAS, { error: 'Elige un momento del día.' }).optional(),
  ),
  hora: horaOpcional,
})

// Pausar exige un motivo (POST /api/my-plans/{id}/transition con nuevoEstado "Pausado").
export const pausarPlanSchema = z.object({
  motivoPausa: z.enum(MOTIVOS_PAUSA, { error: 'Elige el motivo de la pausa.' }),
})

// PUT /api/my-plans/{id}/reminder: para activar los recordatorios hace falta una hora.
export const recordatorioSchema = z
  .object({
    recordatoriosActivos: z.boolean(),
    hora: horaOpcional,
  })
  .refine((datos) => !datos.recordatoriosActivos || datos.hora !== undefined, {
    path: ['hora'],
    message: 'Elige a qué hora quieres el recordatorio.',
  })

export type InscribirseForm = z.infer<typeof inscribirseSchema>
export type PausarPlanForm = z.infer<typeof pausarPlanSchema>
export type RecordatorioForm = z.infer<typeof recordatorioSchema>

// Del formulario a la petición: la hora viaja como "HH:mm:ss" y lo que no se eligió no se envía.
export function aInscribirseRequest(datos: InscribirseForm): InscribirseRequest {
  return {
    slug: datos.slug,
    ...(datos.ancla ? { ancla: datos.ancla } : {}),
    ...(datos.hora ? { hora: horaParaApi(datos.hora) } : {}),
  }
}

export function aPausarRequest(datos: PausarPlanForm): CambiarEstadoPlanRequest {
  return { nuevoEstado: 'Pausado', motivoPausa: datos.motivoPausa }
}

export function aRecordatorioRequest(datos: RecordatorioForm): RecordatorioPlanRequest {
  return {
    hora: datos.hora ? horaParaApi(datos.hora) : null,
    recordatoriosActivos: datos.recordatoriosActivos,
  }
}
