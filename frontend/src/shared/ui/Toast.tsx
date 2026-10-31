import { createContext, useCallback, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'
import { Icono, type NombreIcono } from './Icono'

type TonoToast = 'exito' | 'peligro' | 'info'

interface Toast {
  id: number
  tono: TonoToast
  mensaje: string
}

interface ToastContextValue {
  notificar: (mensaje: string, tono?: TonoToast) => void
}

const ToastContext = createContext<ToastContextValue | null>(null)

const tonos: Record<TonoToast, { clase: string; icono: NombreIcono; color: string }> = {
  exito: { clase: 'border-exito/60', icono: 'check', color: 'text-exito' },
  peligro: { clase: 'border-peligro/60', icono: 'alerta', color: 'text-peligro' },
  info: { clase: 'border-borde-fuerte', icono: 'info', color: 'text-info' },
}

let siguienteId = 1

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])

  const quitar = useCallback((id: number) => {
    setToasts((actuales) => actuales.filter((t) => t.id !== id))
  }, [])

  const notificar = useCallback(
    (mensaje: string, tono: TonoToast = 'exito') => {
      const id = siguienteId++
      setToasts((actuales) => [...actuales, { id, tono, mensaje }])
      // Los errores se quedan hasta que el usuario los descarte (WCAG 2.2.1).
      if (tono !== 'peligro') window.setTimeout(() => quitar(id), 5000)
    },
    [quitar],
  )

  const valor = useMemo(() => ({ notificar }), [notificar])

  return (
    <ToastContext.Provider value={valor}>
      {children}
      <div
        aria-live="polite"
        className="pointer-events-none fixed inset-x-4 bottom-20 z-[60] flex flex-col items-end gap-2 lg:bottom-4"
      >
        {toasts.map((t) => (
          <div
            key={t.id}
            role={t.tono === 'peligro' ? 'alert' : 'status'}
            className={cn(
              'anim-aparecer pointer-events-auto flex w-full max-w-sm items-start gap-3 rounded-lg border bg-superficie px-4 py-3 text-sm text-texto shadow-brillo',
              tonos[t.tono].clase,
            )}
          >
            <Icono
              nombre={tonos[t.tono].icono}
              className={cn('mt-0.5 size-5 shrink-0', tonos[t.tono].color)}
            />
            <span className="flex-1">{t.mensaje}</span>
            <button
              type="button"
              onClick={() => quitar(t.id)}
              aria-label="Descartar"
              className="-m-2 grid size-11 shrink-0 place-items-center rounded-lg text-atenuado hover:text-texto"
            >
              <Icono nombre="cerrar" className="size-4" />
            </button>
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  )
}

export function useToast(): ToastContextValue {
  const contexto = useContext(ToastContext)
  if (!contexto) throw new Error('useToast debe usarse dentro de ToastProvider')
  return contexto
}
