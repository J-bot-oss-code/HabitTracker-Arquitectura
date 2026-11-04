import { z } from 'zod'
import { ACCIONES_AUDITORIA } from './types'

// Límite propuesto para el backend (mejora futura): la auditoría aún no existe en Host/WebApi.
// El correo máximo es el de AccessControl (150 caracteres).
// "" (sin filtro) o un día real del calendario: 2026-13-45 y 2026-02-30 no lo son.
export const esDiaReal = (v: string) => {
  const t = Date.parse(`${v}T00:00:00Z`)
  return Number.isFinite(t) && new Date(t).toISOString().startsWith(v)
}
const dia = z
  .string()
  .regex(/^(\d{4}-\d{2}-\d{2})?$/, 'Elige una fecha válida.')
  .refine((v) => v === '' || esDiaReal(v), 'Elige una fecha válida.')

export const filtroAuditoriaSchema = z
  .object({
    actor: z.string().trim().max(150, 'Admite como máximo 150 caracteres.'),
    // "" = todas las acciones.
    accion: z.union([z.literal(''), z.enum(ACCIONES_AUDITORIA)]),
    desde: dia,
    hasta: dia,
  })
  .refine((f) => !f.desde || !f.hasta || f.desde <= f.hasta, {
    message: '"Desde" no puede ser posterior a "Hasta".',
    path: ['hasta'],
  })

export type FiltroAuditoriaForm = z.infer<typeof filtroAuditoriaSchema>
