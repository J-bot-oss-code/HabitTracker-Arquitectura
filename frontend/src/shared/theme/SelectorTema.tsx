import { useEffect, useId, useRef, useState } from 'react'
import { cn } from '@/shared/lib/cn'
import { Icono } from '@/shared/ui/Icono'
import { useTema } from './ThemeContext'
import { INFO_TEMAS, type InfoTema } from './temas'

function Muestra({ tema }: { tema: InfoTema }) {
  return (
    <span
      aria-hidden="true"
      className="flex h-6 w-10 shrink-0 overflow-hidden rounded-md border border-borde-fuerte"
    >
      <span className="flex-1" style={{ background: tema.muestra[0] }} />
      <span className="flex-1" style={{ background: tema.muestra[1] }} />
      <span className="w-2.5" style={{ background: tema.muestra[2] }} />
    </span>
  )
}

// Menú compacto de la cabecera: botón + lista de radios navegable con flechas.
export function SelectorTemaMenu() {
  const { tema, cambiarTema } = useTema()
  const [abierto, setAbierto] = useState(false)
  const raiz = useRef<HTMLDivElement>(null)
  const idMenu = useId()
  const actual = INFO_TEMAS.find((t) => t.clave === tema) ?? INFO_TEMAS[0]

  useEffect(() => {
    if (!abierto) return
    const fuera = (e: MouseEvent) => {
      if (!raiz.current?.contains(e.target as Node)) setAbierto(false)
    }
    const tecla = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        setAbierto(false)
        raiz.current?.querySelector<HTMLElement>('button[aria-haspopup]')?.focus()
      }
    }
    document.addEventListener('mousedown', fuera)
    document.addEventListener('keydown', tecla)
    return () => {
      document.removeEventListener('mousedown', fuera)
      document.removeEventListener('keydown', tecla)
    }
  }, [abierto])

  const disparador = () => raiz.current?.querySelector<HTMLElement>('button[aria-haspopup]')

  const moverFoco = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Tab') {
      setAbierto(false)
      return
    }
    const items = Array.from(
      raiz.current?.querySelectorAll<HTMLElement>('[role="menuitemradio"]') ?? [],
    )
    if (e.key === 'Home' || e.key === 'End') {
      e.preventDefault()
      items.at(e.key === 'Home' ? 0 : -1)?.focus()
      return
    }
    if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return
    e.preventDefault()
    if (!abierto) {
      setAbierto(true)
      return
    }
    const i = items.indexOf(document.activeElement as HTMLElement)
    const siguiente =
      e.key === 'ArrowDown' ? (i + 1) % items.length : (i - 1 + items.length) % items.length
    items[siguiente]?.focus()
  }

  return (
    <div ref={raiz} className="relative" onKeyDown={moverFoco}>
      <button
        type="button"
        aria-haspopup="menu"
        aria-expanded={abierto}
        aria-controls={abierto ? idMenu : undefined}
        aria-label={`Tema: ${actual.nombre}`}
        onClick={() => setAbierto((v) => !v)}
        className="grid size-11 place-items-center rounded-lg border border-borde-fuerte bg-superficie text-texto hover:border-acento"
      >
        <Icono nombre="tema" />
      </button>
      {abierto && (
        <div
          id={idMenu}
          role="menu"
          aria-label="Tema de la interfaz"
          className="anim-aparecer absolute right-0 z-50 mt-2 w-64 rounded-xl border border-borde-fuerte bg-superficie p-1.5 shadow-brillo"
        >
          {INFO_TEMAS.map((t) => (
            <button
              key={t.clave}
              type="button"
              role="menuitemradio"
              aria-checked={t.clave === tema}
              autoFocus={t.clave === tema}
              onClick={() => {
                cambiarTema(t.clave)
                setAbierto(false)
                // El menú se desmonta: el foco vuelve al botón que lo abrió.
                disparador()?.focus()
              }}
              className={cn(
                'flex min-h-11 w-full items-center gap-3 rounded-lg px-2.5 py-2 text-left text-sm hover:bg-superficie-2',
                t.clave === tema && 'bg-superficie-2 font-semibold',
              )}
            >
              <Muestra tema={t} />
              <span className="flex-1">{t.nombre}</span>
              {t.clave === tema && <Icono nombre="check" className="size-4 text-acento" />}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

// Sección "Apariencia" de Mi cuenta: tarjetas-radio con la muestra de cada tema.
export function SelectorTemaTarjetas() {
  const { tema, cambiarTema } = useTema()
  return (
    <div role="radiogroup" aria-label="Tema de la interfaz" className="grid gap-3 sm:grid-cols-2">
      {INFO_TEMAS.map((t) => (
        <label
          key={t.clave}
          className={cn(
            'flex min-h-11 cursor-pointer items-center gap-3 rounded-xl border p-3 transition-colors has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-foco',
            t.clave === tema
              ? 'border-acento bg-superficie-2 shadow-brillo'
              : 'border-borde-fuerte hover:border-acento',
          )}
        >
          <input
            type="radio"
            name="tema"
            value={t.clave}
            checked={t.clave === tema}
            onChange={() => cambiarTema(t.clave)}
            className="sr-only"
          />
          <Muestra tema={t} />
          <span className="min-w-0 flex-1">
            <span className="block text-sm font-semibold">{t.nombre}</span>
            <span className="block text-xs text-atenuado">{t.descripcion}</span>
          </span>
          {t.clave === tema && <Icono nombre="check" className="size-5 shrink-0 text-acento" />}
        </label>
      ))}
    </div>
  )
}
