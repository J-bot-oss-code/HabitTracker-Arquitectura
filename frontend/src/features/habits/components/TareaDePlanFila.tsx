import { Link } from 'react-router'
import { ETIQUETA_ANCLA } from '@/features/planes/etiquetas'
import type { TareaHoyDto } from '@/features/mis-planes/types'
import { mensajeDeError } from '@/shared/api/problem'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { useToast } from '@/shared/ui/Toast'
import { useRegistrarAvance } from '../api'

const formatoCantidad = new Intl.NumberFormat('es-DO', { maximumFractionDigits: 2 })

// Una tarea de un plan en «Hoy». «Hecho» registra lo que falta del periodo; «Hoy solo lo mínimo» registra la
// versión mínima y también cuenta como día cumplido. Ninguna de las dos se presenta como un fallo (P3).
export function TareaDePlanFila({ tarea }: { tarea: TareaHoyDto }) {
  const registrar = useRegistrarAvance(tarea.habitoId)
  const { notificar } = useToast()

  function registrar_(cantidad: number, esVersionMinima: boolean, aviso: string) {
    registrar.mutate(
      { cantidad, fechaUtc: new Date().toISOString(), esVersionMinima },
      {
        onSuccess: () => notificar(aviso),
        onError: (e) => notificar(mensajeDeError(e, 'No se pudo registrar el avance.'), 'peligro'),
      },
    )
  }

  // shortcut: la versión mínima no tiene cantidad propia en el contrato; se registra la mitad de lo que falta
  // (máximo 1), siempre por debajo de la meta, hasta que el backend la defina.
  const cantidadMinima = Math.max(0.01, Math.round(Math.min(1, tarea.restante / 2) * 100) / 100)
  const momento = [
    tarea.ancla ? ETIQUETA_ANCLA[tarea.ancla] : null,
    tarea.horaSugerida ? `${tarea.horaSugerida}` : null,
  ]
    .filter(Boolean)
    .join(' · ')

  return (
    <li className="space-y-2 py-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="min-w-0">
          <Link
            to={`/habitos/${tarea.habitoId}`}
            className="flex min-h-11 items-center font-semibold hover:underline"
          >
            {tarea.nombre}
          </Link>
          <p className="text-sm text-atenuado">
            {tarea.planNombre}
            {momento && ` · ${momento}`}
          </p>
          <p className="text-sm text-atenuado">
            {tarea.periodicidad === 'Diaria' ? 'Diario' : 'Semanal'} ·{' '}
            <span className="tabular-nums">
              {formatoCantidad.format(tarea.cantidadPeriodo)} /{' '}
              {formatoCantidad.format(tarea.cantidadObjetivo)}
            </span>{' '}
            {tarea.unidad}
          </p>
        </div>
        {tarea.estado === 'Hecho' && (
          <Badge tono="exito" icono="check">
            Hecho
          </Badge>
        )}
        {tarea.estado === 'Minimo' && (
          <Badge tono="exito" icono="check">
            Hoy hiciste lo mínimo
          </Badge>
        )}
      </div>
      {tarea.instrucciones && tarea.estado === 'Pendiente' && (
        <p className="text-sm text-texto-2">{tarea.instrucciones}</p>
      )}
      {tarea.estado !== 'Hecho' && (
        <div className="flex flex-wrap gap-2">
          <Button
            tamano="sm"
            cargando={registrar.isPending}
            onClick={() => registrar_(tarea.restante, false, `¡${tarea.nombre} hecho!`)}
            aria-label={`Hecho: ${tarea.nombre}`}
          >
            Hecho
          </Button>
          {tarea.estado === 'Pendiente' && tarea.versionMinima && (
            <Button
              tamano="sm"
              variante="secundario"
              disabled={registrar.isPending}
              onClick={() =>
                registrar_(cantidadMinima, true, 'Con lo mínimo también cuenta. ¡Bien hecho!')
              }
              aria-label={`Hoy solo lo mínimo: ${tarea.nombre}`}
            >
              Hoy solo lo mínimo
            </Button>
          )}
        </div>
      )}
      {tarea.estado === 'Pendiente' && tarea.versionMinima && (
        <p className="text-xs text-atenuado">Lo mínimo: {tarea.versionMinima}</p>
      )}
    </li>
  )
}
