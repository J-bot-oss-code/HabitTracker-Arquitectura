import { Badge, type Tono } from '@/shared/ui/Badge'
import type { NombreIcono } from '@/shared/ui/Icono'
import type { EstadoSugerencia } from '../types'

// El estado nunca depende solo del color: lleva texto y, el resuelto, una marca.
const PRESENTACION: Record<EstadoSugerencia, { tono: Tono; icono?: NombreIcono }> = {
  Nueva: { tono: 'marca' },
  Leida: { tono: 'info' },
  Resuelta: { tono: 'exito', icono: 'check' },
}

export function BadgeEstadoSugerencia({
  estado,
  rotulo,
}: {
  estado: EstadoSugerencia
  rotulo: string
}) {
  const p = PRESENTACION[estado]
  return (
    <Badge tono={p.tono} icono={p.icono}>
      {rotulo}
    </Badge>
  )
}
