import { Badge } from '@/shared/ui/Badge'
import { presentacionEstado } from '../estados'
import type { EstadoHabito } from '../types'

export function EstadoHabitoBadge({ estado }: { estado: EstadoHabito }) {
  const { etiqueta, tono } = presentacionEstado[estado]
  return <Badge tono={tono}>{etiqueta}</Badge>
}
