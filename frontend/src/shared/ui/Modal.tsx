import { useEffect, useId, useRef } from 'react'
import type { ReactNode } from 'react'
import { Icono } from './Icono'

const FOCALIZABLES =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

export function Modal({
  abierto,
  titulo,
  alCerrar,
  children,
}: {
  abierto: boolean
  titulo: string
  alCerrar: () => void
  children: ReactNode
}) {
  const tituloId = useId()
  const panel = useRef<HTMLDivElement>(null)
  // El callback puede cambiar de identidad en cada render del padre; el efecto no debe
  // reiniciarse (y robar el foco) por eso.
  const cerrar = useRef(alCerrar)
  useEffect(() => {
    cerrar.current = alCerrar
  })

  useEffect(() => {
    if (!abierto) return
    const anterior = document.activeElement as HTMLElement | null
    // Foco inicial en el primer campo del contenido (no en el botón de cerrar).
    const dentro = panel.current?.querySelectorAll<HTMLElement>(FOCALIZABLES)
    const primerCampo = Array.from(dentro ?? []).find((el) => el.dataset.cerrar === undefined)
    ;(primerCampo ?? panel.current)?.focus()

    const alPresionar = (evento: KeyboardEvent) => {
      if (evento.key === 'Escape') {
        cerrar.current()
        return
      }
      // Atrapa el foco dentro del diálogo mientras está abierto.
      if (evento.key !== 'Tab' || !panel.current) return
      const items = Array.from(panel.current.querySelectorAll<HTMLElement>(FOCALIZABLES))
      if (items.length === 0) return
      const primero = items[0]
      const ultimo = items[items.length - 1]
      if (evento.shiftKey && document.activeElement === primero) {
        evento.preventDefault()
        ultimo.focus()
      } else if (!evento.shiftKey && document.activeElement === ultimo) {
        evento.preventDefault()
        primero.focus()
      }
    }
    document.addEventListener('keydown', alPresionar)
    return () => {
      document.removeEventListener('keydown', alPresionar)
      // Al cerrar, el foco vuelve al control que abrió el diálogo.
      anterior?.focus?.()
    }
  }, [abierto])

  if (!abierto) return null

  return (
    <div className="fixed inset-0 z-50 flex items-end justify-center p-4 sm:items-center">
      <div
        className="absolute inset-0 bg-fondo/80 backdrop-blur-sm"
        aria-hidden="true"
        onClick={alCerrar}
      />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-labelledby={tituloId}
        tabIndex={-1}
        className="anim-aparecer relative max-h-[90vh] w-full max-w-lg overflow-y-auto rounded-2xl border border-borde-fuerte bg-superficie p-6 shadow-brillo"
      >
        <div className="mb-4 flex items-start justify-between gap-4">
          <h2 id={tituloId} className="text-2xl font-bold uppercase tracking-wide">
            {titulo}
          </h2>
          <button
            type="button"
            data-cerrar
            onClick={alCerrar}
            aria-label="Cerrar"
            className="grid size-11 shrink-0 place-items-center rounded-lg text-atenuado hover:bg-superficie-2 hover:text-texto"
          >
            <Icono nombre="cerrar" />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}
