import { Badge } from '@/shared/ui/Badge'
import { iconoEstado, presentacionEstado } from '../estados'
import type { EstadoHabito } from '../types'

export function EstadoHabitoBadge({ estado }: { estado: EstadoHabito }) {
  const { etiqueta, tono } = presentacionEstado[estado]
  return (
    <Badge tono={tono} icono={iconoEstado[estado]}>
      {etiqueta}
    </Badge>
  )
}
