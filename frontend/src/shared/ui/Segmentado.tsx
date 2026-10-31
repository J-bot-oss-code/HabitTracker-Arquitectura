import { cn } from '@/shared/lib/cn'

export interface OpcionSegmentada<T extends string | boolean> {
  valor: T
  etiqueta: string
  // Se omite mientras no haya datos para no mostrar "(0)" durante la carga.
  contador?: number
}

// Filtro de una sola selección: grupo de botones con aria-pressed (no son pestañas, porque
// no controlan paneles distintos). Un único componente para todos los filtros de la app.
export function Segmentado<T extends string | boolean>({
  etiqueta,
  opciones,
  valor,
  alCambiar,
}: {
  etiqueta: string
  opciones: OpcionSegmentada<T>[]
  valor: T
  alCambiar: (valor: T) => void
}) {
  return (
    <div role="group" aria-label={etiqueta} className="flex flex-wrap gap-2">
      {opciones.map((o) => (
        <button
          key={String(o.valor)}
          type="button"
          aria-pressed={o.valor === valor}
          onClick={() => alCambiar(o.valor)}
          className={cn(
            'min-h-11 rounded-full border px-4 text-sm font-semibold transition-colors',
            o.valor === valor
              ? 'border-acento bg-acento text-acento-texto'
              : 'border-borde-fuerte bg-superficie text-texto hover:border-acento',
          )}
        >
          {o.etiqueta}
          {o.contador !== undefined && (
            <>
              {' '}
              <span className="font-normal">({o.contador})</span>
            </>
          )}
        </button>
      ))}
    </div>
  )
}
