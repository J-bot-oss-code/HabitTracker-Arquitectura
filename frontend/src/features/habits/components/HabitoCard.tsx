import { Link } from 'react-router'
import { Card, CardBody } from '@/shared/ui/Card'
import { avanceDelPeriodo, etiquetaPeriodo } from '../estados'
import type { HabitoDto } from '../types'
import { BarraProgreso } from './BarraProgreso'
import { EstadoHabitoBadge } from './EstadoHabitoBadge'

export function HabitoCard({ habito }: { habito: HabitoDto }) {
  return (
    <Card className="transition-shadow hover:shadow-md">
      <Link to={`/habitos/${habito.id}`} className="block rounded-xl">
        <CardBody className="space-y-4">
          <div className="flex items-start justify-between gap-3">
            <div className="min-w-0">
              <h3 className="truncate font-semibold">{habito.nombre}</h3>
              <p className="text-sm text-slate-500">{habito.periodicidad}</p>
            </div>
            <EstadoHabitoBadge estado={habito.estado} />
          </div>
          <BarraProgreso
            actual={avanceDelPeriodo(habito)}
            objetivo={habito.cantidadObjetivo}
            unidad={habito.unidad}
            periodo={etiquetaPeriodo[habito.periodicidad]}
            cumplida={habito.cumpleMetaFinal}
          />
        </CardBody>
      </Link>
    </Card>
  )
}
