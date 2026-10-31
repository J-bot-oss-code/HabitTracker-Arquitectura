import { ComingSoon } from '@/shared/ui/ComingSoon'
import type { PiezaPendiente } from './piezas'

export function ComingSoonPage({ pieza }: { pieza: PiezaPendiente }) {
  return (
    <ComingSoon
      titulo={pieza.titulo}
      descripcion={pieza.descripcion}
      entrega={pieza.entrega}
      requisitos={pieza.requisitos}
    />
  )
}
