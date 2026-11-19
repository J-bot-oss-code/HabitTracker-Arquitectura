import { hoyUtcInput } from '@/shared/lib/fechas'
import type { RangoReporte } from './types'

const MS_DIA = 86_400_000

// Los últimos `dias` días terminando hoy (UTC, RD-11), ambos extremos incluidos: 7 días = hoy y los 6 anteriores.
export function rangoUltimosDias(dias: number, ahora = new Date()): RangoReporte {
  const hasta = hoyUtcInput(ahora)
  const desde = new Date(Date.parse(`${hasta}T00:00:00Z`) - (dias - 1) * MS_DIA)
  return { desde: desde.toISOString().slice(0, 10), hasta }
}
