import { cn } from '@/shared/lib/cn'

export type NivelCalor = 0 | 1 | 2 | 3

export interface CeldaCalor {
  // Clave estable (p. ej. "2026-10-30") y texto accesible de la celda.
  clave: string
  nivel: NivelCalor
  descripcion: string
  hoy?: boolean
  // Día que aún no llega: se dibuja punteado para no confundirlo con "no lo hice".
  futuro?: boolean
}

// Nivel 0 sin relleno y con borde: los rellenos claros se confundían con la superficie. Los
// niveles 1 a 3 son cada vez más oscuros, con saltos de al menos 2:1 (lo vigila
// contraste.test.ts).
const estilo: Record<NivelCalor, string> = {
  0: 'border border-control',
  1: 'bg-calor-1',
  2: 'bg-calor-2',
  3: 'bg-calor-3',
}

const textoNivel: Record<NivelCalor, string> = {
  0: 'Sin avances',
  1: 'Poco',
  2: 'Bastante',
  3: 'Todo hecho',
}

// HeatmapCalendar de la especificación (§3): rejilla de días con 4 niveles de intensidad. Los
// niveles no se distinguen solo por color (WCAG 1.4.1): el nivel 0 tiene borde, cada celda dice
// su descripción como texto para lectores de pantalla y la leyenda nombra los niveles.
export function CalendarioCalor({
  celdas,
  columnas,
  etiqueta,
  etiquetasFilas,
}: {
  // En orden cronológico; se rellenan por columnas (cada columna es una semana).
  celdas: CeldaCalor[]
  columnas: number
  etiqueta: string
  // Siete rótulos de lunes a domingo para el borde izquierdo (opcional).
  etiquetasFilas?: readonly string[]
}) {
  return (
    <figure>
      <div className="flex gap-2">
        {etiquetasFilas && (
          <div
            aria-hidden="true"
            className="grid grid-rows-7 gap-1 text-xs font-medium leading-none text-atenuado"
          >
            {etiquetasFilas.map((e, i) => (
              <span key={i} className="flex items-center">
                {e}
              </span>
            ))}
          </div>
        )}
        <ol
          role="list"
          aria-label={etiqueta}
          className="grid min-w-0 flex-1 grid-flow-col grid-rows-7 gap-1"
          style={{ gridTemplateColumns: `repeat(${columnas}, minmax(0, 1fr))` }}
        >
          {celdas.map((c) => (
            <li
              key={c.clave}
              className={cn(
                'aspect-square min-h-3 rounded-[4px]',
                c.futuro ? 'border border-dashed border-borde-fuerte' : estilo[c.nivel],
                c.hoy && 'ring-2 ring-foco ring-offset-1 ring-offset-superficie',
              )}
            >
              <span className="sr-only">
                {c.descripcion}
                {c.hoy ? ' (hoy)' : ''}
              </span>
            </li>
          ))}
        </ol>
      </div>
      <figcaption className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-1.5 text-xs text-atenuado">
        {([0, 1, 2, 3] as const).map((n) => (
          <span key={n} className="inline-flex items-center gap-1.5 whitespace-nowrap">
            <span aria-hidden="true" className={cn('size-3 rounded-[3px]', estilo[n])} />
            {textoNivel[n]}
          </span>
        ))}
        <span className="inline-flex items-center gap-1.5 whitespace-nowrap">
          <span
            aria-hidden="true"
            className="size-3 rounded-[3px] border border-dashed border-borde-fuerte"
          />
          Aún no llega
        </span>
      </figcaption>
    </figure>
  )
}
