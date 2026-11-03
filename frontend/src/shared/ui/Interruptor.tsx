import { useId } from 'react'
import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'

// Switch de la especificación (§3) con role="switch" y aria-checked. El estado se lee también
// en el texto "Activado"/"Desactivado", no solo en el color ni en la posición de la perilla.
// El objetivo táctil es de 44 px de alto y la etiqueta es parte del control.
export function Interruptor({
  etiqueta,
  descripcion,
  activado,
  alCambiar,
  deshabilitado = false,
}: {
  etiqueta: string
  descripcion?: ReactNode
  activado: boolean
  alCambiar: (nuevo: boolean) => void
  deshabilitado?: boolean
}) {
  const idDescripcion = useId()
  return (
    <div className="flex items-start justify-between gap-4 py-3">
      <div className="min-w-0">
        <p className="font-semibold">{etiqueta}</p>
        {descripcion && (
          <p id={idDescripcion} className="mt-0.5 text-sm text-atenuado">
            {descripcion}
          </p>
        )}
      </div>
      <button
        type="button"
        role="switch"
        aria-checked={activado}
        aria-label={etiqueta}
        aria-describedby={descripcion ? idDescripcion : undefined}
        // aria-disabled (no disabled): un botón deshabilitado pierde el foco y desorienta a quien
        // navega con teclado o lector mientras se guarda.
        aria-disabled={deshabilitado || undefined}
        onClick={() => {
          if (!deshabilitado) alCambiar(!activado)
        }}
        className={cn(
          'group flex min-h-11 shrink-0 items-center gap-2 rounded-lg px-1',
          deshabilitado && 'cursor-not-allowed opacity-60',
        )}
      >
        <span className="w-24 text-right text-xs font-semibold text-atenuado">
          {activado ? 'Activado' : 'Desactivado'}
        </span>
        <span
          aria-hidden="true"
          className={cn(
            'relative inline-flex h-6 w-11 shrink-0 items-center rounded-full border-2 transition-colors',
            activado ? 'border-acento bg-acento' : 'border-control bg-superficie',
          )}
        >
          <span
            className={cn(
              'inline-block size-4 rounded-full transition-transform',
              activado ? 'translate-x-[1.375rem] bg-acento-texto' : 'translate-x-0.5 bg-control',
            )}
          />
        </span>
      </button>
    </div>
  )
}
