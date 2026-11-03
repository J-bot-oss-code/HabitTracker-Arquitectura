import { cn } from '@/shared/lib/cn'
import { Icono } from './Icono'

export type EstadoCelda = 'completo' | 'parcial' | 'vacio' | 'futuro'

export interface CeldaSemana {
  // Abreviatura visible ("L", "M"…) y nombre completo para lectores de pantalla.
  corta: string
  larga: string
  estado: EstadoCelda
  hoy?: boolean
}

const estilo: Record<EstadoCelda, string> = {
  completo: 'bg-acento text-acento-texto',
  parcial: 'bg-acento-tinte text-acento-fuerte',
  vacio: 'border border-borde-fuerte bg-superficie text-atenuado',
  futuro: 'border-2 border-dashed border-borde-fuerte text-atenuado',
}

const texto: Record<EstadoCelda, string> = {
  completo: 'todo hecho',
  parcial: 'en parte',
  vacio: 'sin avances',
  futuro: 'aún no llega',
}

// WeekStrip de la especificación (§3): siete celdas de lunes a domingo. El estado se lee en el
// texto de cada celda (no solo en el color) y en el icono del día completo.
export function TiraSemana({ dias, etiqueta }: { dias: CeldaSemana[]; etiqueta: string }) {
  return (
    <ol aria-label={etiqueta} className="grid grid-cols-7 gap-1.5">
      {dias.map((d) => (
        <li key={d.larga} className="flex flex-col items-center gap-1">
          <span
            aria-hidden="true"
            className={cn(
              'grid aspect-square w-full max-w-10 place-items-center rounded-lg text-xs font-bold',
              estilo[d.estado],
              d.hoy && 'ring-2 ring-foco ring-offset-2 ring-offset-superficie',
            )}
          >
            {d.estado === 'completo' && <Icono nombre="check" className="size-4" />}
            {/* Cada estado tiene su forma: check, guion, vacío liso o borde punteado. */}
            {d.estado === 'parcial' && '–'}
          </span>
          <span
            aria-hidden="true"
            className={cn('text-xs text-atenuado', d.hoy ? 'font-bold text-texto' : 'font-medium')}
          >
            {d.corta}
          </span>
          <span className="sr-only">
            {d.larga}
            {d.hoy ? ' (hoy)' : ''}: {texto[d.estado]}
          </span>
        </li>
      ))}
    </ol>
  )
}
