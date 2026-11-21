import { z } from 'zod'
import { ACCIONES_AUDITORIA } from './types'

// Límites de GET /api/audit (RegistroAuditoriaService): usuario hasta 254 caracteres y, si se dan
// ambos días, hasta 366 días entre los dos (ambos incluidos).
export const MAXIMO_DIAS_RANGO = 366
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
    usuario: z.string().trim().max(254, 'Admite como máximo 254 caracteres.'),
    // "" = todas las acciones.
    accion: z.union([z.literal(''), z.enum(ACCIONES_AUDITORIA)]),
    desde: dia,
    hasta: dia,
  })
  .refine((f) => !f.desde || !f.hasta || f.desde <= f.hasta, {
    message: '"Desde" no puede ser posterior a "Hasta".',
    path: ['hasta'],
  })
  .refine(
    (f) =>
      !f.desde ||
      !f.hasta ||
      f.desde > f.hasta ||
      (Date.parse(`${f.hasta}T00:00:00Z`) - Date.parse(`${f.desde}T00:00:00Z`)) / 86_400_000 + 1 <=
        MAXIMO_DIAS_RANGO,
    { message: `El rango no puede pasar de ${MAXIMO_DIAS_RANGO} días.`, path: ['hasta'] },
  )

export type FiltroAuditoriaForm = z.infer<typeof filtroAuditoriaSchema>
