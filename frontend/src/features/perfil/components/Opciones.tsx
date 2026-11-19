import { useId } from 'react'
import { cn } from '@/shared/lib/cn'
import { Icono } from '@/shared/ui/Icono'

// Tarjetas de opción única (radio) con la misma apariencia que el selector de tema. Todo el
// renglón es pulsable (44 px de alto mínimo) y el foco se ve con el contorno del tema.
export function OpcionesTarjeta<T extends string>({
  leyenda,
  opciones,
  valor,
  alCambiar,
  error,
  columnas = 1,
}: {
  leyenda: string
  opciones: readonly { valor: T; titulo: string; detalle?: string }[]
  valor: T | undefined | null
  alCambiar: (valor: T) => void
  error?: string
  columnas?: 1 | 2
}) {
  const nombre = useId()
  const idError = `${nombre}-error`
  return (
    <fieldset
      aria-invalid={error ? true : undefined}
      aria-describedby={error ? idError : undefined}
    >
      <legend className="mb-2 text-sm font-semibold text-texto">{leyenda}</legend>
      <div className={cn('grid gap-2', columnas === 2 && 'sm:grid-cols-2')}>
        {opciones.map((o) => {
          const elegida = o.valor === valor
          return (
            <label
              key={o.valor}
              className={cn(
                'flex min-h-11 cursor-pointer items-center gap-3 rounded-xl border p-3 transition-colors has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-foco',
                elegida
                  ? 'border-acento bg-acento-suave ring-1 ring-acento'
                  : 'border-borde-fuerte hover:border-acento',
              )}
            >
              <input
                type="radio"
                name={nombre}
                value={o.valor}
                checked={elegida}
                onChange={() => alCambiar(o.valor)}
                className="sr-only"
              />
              <span className="min-w-0 flex-1">
                <span className="block text-sm font-semibold">{o.titulo}</span>
                {o.detalle && <span className="block text-xs text-atenuado">{o.detalle}</span>}
              </span>
              {elegida && <Icono nombre="check" className="size-5 shrink-0 text-acento" />}
            </label>
          )
        })}
      </div>
      {error && (
        <p id={idError} role="alert" className="mt-1 text-xs font-medium text-peligro">
          {error}
        </p>
      )}
    </fieldset>
  )
}

// Casillas de opción múltiple. Si existe la opción "Ninguna", es exclusiva: marcarla desmarca las
// demás y marcar otra la desmarca (mismo criterio que el backend). `maximo` limita la selección.
export function OpcionesMultiples<T extends string>({
  leyenda,
  ayuda,
  opciones,
  valores,
  alCambiar,
  error,
  maximo,
}: {
  leyenda: string
  ayuda?: string
  opciones: readonly { valor: T; titulo: string }[]
  valores: readonly T[]
  alCambiar: (valores: T[]) => void
  error?: string
  maximo?: number
}) {
  const alternar = (v: T) => {
    if (valores.includes(v)) return alCambiar(valores.filter((x) => x !== v))
    if (v === 'Ninguna') return alCambiar([v])
    const sinNinguna = valores.filter((x) => x !== 'Ninguna')
    if (maximo && sinNinguna.length >= maximo) return
    alCambiar([...sinNinguna, v])
  }
  const lleno = maximo !== undefined && valores.length >= maximo
  const idError = `${useId()}-error`
  return (
    <fieldset
      aria-invalid={error ? true : undefined}
      aria-describedby={error ? idError : undefined}
    >
      <legend className="text-sm font-semibold text-texto">{leyenda}</legend>
      {ayuda && <p className="mb-2 text-xs text-atenuado">{ayuda}</p>}
      <div className="grid gap-1 sm:grid-cols-2">
        {opciones.map((o) => {
          const marcada = valores.includes(o.valor)
          return (
            <label key={o.valor} className="flex min-h-11 cursor-pointer items-center gap-3 py-1">
              <input
                type="checkbox"
                checked={marcada}
                disabled={!marcada && lleno}
                onChange={() => alternar(o.valor)}
                className="size-5 shrink-0 cursor-pointer rounded accent-acento disabled:cursor-not-allowed"
              />
              <span className="text-sm leading-snug text-texto-2">{o.titulo}</span>
            </label>
          )
        })}
      </div>
      {error && (
        <p id={idError} role="alert" className="mt-1 text-xs font-medium text-peligro">
          {error}
        </p>
      )}
    </fieldset>
  )
}
