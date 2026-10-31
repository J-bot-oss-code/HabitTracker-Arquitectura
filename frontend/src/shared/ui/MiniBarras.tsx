import { cn } from '@/shared/lib/cn'

export interface BarraDia {
  etiqueta: string
  valor: number
}

// Barras de los últimos días en HTML/CSS, sin librería. Para lectores de pantalla el gráfico
// es una sola imagen con el resumen; la línea punteada marca el objetivo, de modo que "cumple"
// no depende solo del color.
export function MiniBarras({
  datos,
  objetivo,
  resumen,
}: {
  datos: BarraDia[]
  objetivo?: number
  resumen: string
}) {
  const maximo = Math.max(objetivo ?? 0, ...datos.map((d) => d.valor), 1)
  const alturaObjetivo = objetivo !== undefined ? (objetivo / maximo) * 100 : undefined

  return (
    <figure>
      <div role="img" aria-label={resumen}>
        <div className="relative flex h-24 items-end gap-2">
          {alturaObjetivo !== undefined && (
            <div
              aria-hidden="true"
              className="pointer-events-none absolute inset-x-0 border-t-2 border-dashed border-atenuado"
              style={{ bottom: `${alturaObjetivo}%` }}
            >
              <span className="absolute -top-5 right-0 rounded bg-superficie px-1 text-xs font-semibold text-atenuado">
                Meta
              </span>
            </div>
          )}
          {datos.map((d, i) => {
            const alto = Math.round((d.valor / maximo) * 100)
            const cumple = objetivo !== undefined && d.valor >= objetivo
            return (
              <div
                key={`${d.etiqueta}-${i}`}
                className={cn(
                  'flex-1 rounded-t-md transition-[height] duration-500',
                  d.valor === 0 ? 'bg-superficie-2' : cumple ? 'bg-exito' : 'bg-acento',
                )}
                style={{ height: d.valor === 0 ? '6%' : `${Math.max(alto, 8)}%` }}
              />
            )
          })}
        </div>
        <div className="mt-1.5 flex gap-2">
          {datos.map((d, i) => (
            <span
              key={`${d.etiqueta}-${i}`}
              className="flex-1 text-center text-xs font-medium uppercase text-atenuado"
            >
              {d.etiqueta}
            </span>
          ))}
        </div>
      </div>
      <figcaption className="sr-only">{resumen}</figcaption>
    </figure>
  )
}
