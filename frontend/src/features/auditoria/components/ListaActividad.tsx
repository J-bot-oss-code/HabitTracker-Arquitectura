import { parsearFechaUtc } from '@/shared/lib/fechas'
import { detalleParaPersona } from '../detalle'
import type { EventoAuditoriaDto } from '../types'

// «Mi actividad» muestra la hora de quien la mira (la del dispositivo); los archivos que se guardan llevan
// el instante UTC. "1 nov 2026, 15:27", en 24 horas.
const formatoCuando = new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium',
  timeStyle: 'short',
  hourCycle: 'h23',
})

// Lista reglada, del más reciente al más antiguo: la hora al margen y, al lado, qué pasó. En móvil se
// apila (hora arriba, acción debajo) y nunca desborda el ancho. No hay columna de usuario: siempre es la
// propia persona; el detalle se lee como frase («Antes: X. Ahora: Y»), no como flecha. El texto se pinta tal cual (React lo escapa): el contenido de un evento nunca es HTML.
export function ListaActividad({ eventos }: { eventos: EventoAuditoriaDto[] }) {
  return (
    <ol
      aria-label="Tu actividad, de la más reciente a la más antigua"
      className="divide-y divide-borde overflow-hidden rounded-2xl border border-borde bg-superficie"
    >
      {eventos.map((e) => {
        const detalle = detalleParaPersona(e)
        return (
          <li
            key={e.id}
            className="grid gap-1 px-4 py-3 sm:grid-cols-[11rem_minmax(0,1fr)] sm:gap-4"
          >
            <time dateTime={e.fecha} className="text-sm tabular-nums text-atenuado sm:pt-px">
              {formatoCuando.format(parsearFechaUtc(e.fecha))}
            </time>
            <div className="min-w-0">
              <p className="break-words font-semibold">{e.accion}</p>
              {detalle && <p className="mt-0.5 break-words text-sm text-texto-2">{detalle}</p>}
            </div>
          </li>
        )
      })}
    </ol>
  )
}
