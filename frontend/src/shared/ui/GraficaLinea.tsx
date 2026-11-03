import { useEffect, useId, useRef, useState } from 'react'

export interface PuntoGrafica {
  // Instante en milisegundos (UTC) y valor.
  t: number
  v: number
}

const ANCHO_POR_DEFECTO = 640
const ANCHO_MINIMO = 280
const ANCHO_MAXIMO = 760
const MARGEN = { arriba: 16, derecha: 18, abajo: 30, izquierda: 46 }

// Ancho real del contenedor: el SVG se dibuja en píxeles reales (1 unidad = 1 px), así el
// texto de los ejes mide siempre 12 px en lugar de encogerse con el viewBox en un móvil.
function useAncho() {
  const ref = useRef<HTMLDivElement>(null)
  const [ancho, setAncho] = useState(ANCHO_POR_DEFECTO)
  useEffect(() => {
    const el = ref.current
    if (!el || typeof ResizeObserver === 'undefined') return
    const medir = () => {
      const w = el.clientWidth
      if (w > 0) setAncho(Math.min(ANCHO_MAXIMO, Math.max(ANCHO_MINIMO, Math.round(w))))
    }
    medir()
    const observador = new ResizeObserver(medir)
    observador.observe(el)
    return () => observador.disconnect()
  }, [])
  return { ref, ancho }
}

// LineChart de la especificación (§3): serie "Real" en línea continua y, si existe, la del
// "Plan" discontinua. Es SVG sin librería. El gráfico entero es una imagen con la conclusión
// como nombre (§7) y los datos exactos van aparte en una tabla para lectores de pantalla.
export function GraficaLinea({
  real,
  plan,
  conclusion,
  formatoValor,
  formatoFecha,
  unidad,
}: {
  real: PuntoGrafica[]
  plan?: PuntoGrafica[]
  // Lo que dice el gráfico, p. ej. "Peso: de 71 a 68,4 kg en 10 semanas".
  conclusion: string
  formatoValor: (v: number) => string
  formatoFecha: (t: number) => string
  unidad: string
}) {
  const idTabla = useId()
  const { ref, ancho } = useAncho()
  // Un punto no finito (fecha o valor corruptos) rompería toda la escala: se descarta.
  const finito = (p: PuntoGrafica) => Number.isFinite(p.t) && Number.isFinite(p.v)
  real = real.filter(finito)
  plan = plan?.filter(finito)
  const todos = [...real, ...(plan ?? [])]
  if (todos.length === 0) return null

  // Más alta en móvil: con 280 px de ancho una gráfica baja aplana la tendencia.
  const alto = ancho < 480 ? 220 : 260
  const t0 = Math.min(...todos.map((p) => p.t))
  const t1 = Math.max(...todos.map((p) => p.t))
  const vMin = Math.min(...todos.map((p) => p.v))
  const vMax = Math.max(...todos.map((p) => p.v))
  // Un poco de aire arriba y abajo; con un solo valor se abre un rango mínimo.
  const holgura = Math.max((vMax - vMin) * 0.15, 0.5)
  const desde = vMin - holgura
  const hasta = vMax + holgura

  const anchoUtil = ancho - MARGEN.izquierda - MARGEN.derecha
  const altoUtil = alto - MARGEN.arriba - MARGEN.abajo
  const x = (t: number) =>
    MARGEN.izquierda + (t1 === t0 ? anchoUtil / 2 : ((t - t0) / (t1 - t0)) * anchoUtil)
  const y = (v: number) => MARGEN.arriba + altoUtil - ((v - desde) / (hasta - desde)) * altoUtil
  const trazo = (puntos: PuntoGrafica[]) =>
    puntos
      .map((p, i) => `${i === 0 ? 'M' : 'L'}${x(p.t).toFixed(1)} ${y(p.v).toFixed(1)}`)
      .join(' ')

  const marcasY = [desde + holgura * 0.2, (desde + hasta) / 2, hasta - holgura * 0.2]
  const marcasX = t1 === t0 ? [t0] : [t0, (t0 + t1) / 2, t1]
  const ultimo = real.length > 0 ? real[real.length - 1] : undefined

  return (
    <figure>
      <div ref={ref} role="img" aria-label={conclusion} aria-describedby={idTabla}>
        <svg
          viewBox={`0 0 ${ancho} ${alto}`}
          width={ancho}
          height={alto}
          className="block max-w-full"
          aria-hidden="true"
        >
          {marcasY.map((v) => (
            <g key={v}>
              <line
                x1={MARGEN.izquierda}
                x2={ancho - MARGEN.derecha}
                y1={y(v)}
                y2={y(v)}
                className="stroke-borde"
                strokeWidth="1"
              />
              <text
                x={MARGEN.izquierda - 8}
                y={y(v)}
                textAnchor="end"
                dominantBaseline="middle"
                className="fill-atenuado"
                fontSize="12"
              >
                {formatoValor(v)}
              </text>
            </g>
          ))}
          {marcasX.map((t, i) => (
            <text
              key={t}
              x={x(t)}
              y={alto - 8}
              textAnchor={
                marcasX.length === 1
                  ? 'middle'
                  : i === 0
                    ? 'start'
                    : i === marcasX.length - 1
                      ? 'end'
                      : 'middle'
              }
              className="fill-atenuado"
              fontSize="12"
            >
              {formatoFecha(t)}
            </text>
          ))}
          {plan && plan.length > 1 && (
            <path
              d={trazo(plan)}
              fill="none"
              className="stroke-control"
              strokeWidth="2"
              strokeDasharray="6 6"
            />
          )}
          {real.length > 1 && (
            <path
              d={trazo(real)}
              fill="none"
              className="stroke-acento"
              strokeWidth="3"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          )}
          {real.map((p, i) => (
            <circle
              key={`${p.t}-${i}`}
              cx={x(p.t)}
              cy={y(p.v)}
              r="4"
              className="fill-acento stroke-superficie"
              strokeWidth="2"
            />
          ))}
          {/* El último punto lleva su valor: es el dato que más importa de la gráfica. */}
          {ultimo && (
            <text
              x={x(ultimo.t)}
              y={y(ultimo.v) - 10}
              textAnchor={x(ultimo.t) > ancho - 50 ? 'end' : 'middle'}
              className="fill-texto"
              fontSize="12"
              fontWeight="700"
            >
              {formatoValor(ultimo.v)}
            </text>
          )}
        </svg>
      </div>

      <figcaption className="mt-2 flex flex-wrap gap-x-5 gap-y-1 text-sm text-atenuado">
        <span className="inline-flex items-center gap-2">
          <span aria-hidden="true" className="h-0.5 w-6 rounded bg-acento" />
          Real
        </span>
        {plan && plan.length > 1 && (
          <span className="inline-flex items-center gap-2">
            <span aria-hidden="true" className="w-6 border-t-2 border-dashed border-control" />
            Plan
          </span>
        )}
      </figcaption>

      <table id={idTabla} className="sr-only">
        <caption>{conclusion}</caption>
        <thead>
          <tr>
            <th scope="col">Fecha</th>
            <th scope="col">Real ({unidad})</th>
          </tr>
        </thead>
        <tbody>
          {real.map((p, i) => (
            <tr key={`${p.t}-${i}`}>
              <td>{formatoFecha(p.t)}</td>
              <td>{formatoValor(p.v)}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </figure>
  )
}
