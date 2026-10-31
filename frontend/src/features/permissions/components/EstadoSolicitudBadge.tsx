import { Badge } from '@/shared/ui/Badge'
import { presentacionSolicitud } from '../catalogo'
import type { EstadoSolicitudPermiso } from '../types'

export function EstadoSolicitudBadge({ estado }: { estado: EstadoSolicitudPermiso }) {
  const { etiqueta, tono } = presentacionSolicitud[estado]
  return <Badge tono={tono}>{etiqueta}</Badge>
}
