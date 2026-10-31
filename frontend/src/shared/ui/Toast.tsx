import { createContext, useCallback, useContext, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'

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

const tonos: Record<TonoToast, string> = {
  exito:
    'border-emerald-300 bg-emerald-50 text-emerald-900 dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-100',
  peligro:
    'border-rose-300 bg-rose-50 text-rose-900 dark:border-rose-800 dark:bg-rose-950 dark:text-rose-100',
  info: 'border-slate-300 bg-white text-slate-900 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100',
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
      window.setTimeout(() => quitar(id), 4000)
    },
    [quitar],
  )

  const valor = useMemo(() => ({ notificar }), [notificar])

  return (
    <ToastContext.Provider value={valor}>
      {children}
      <div
        aria-live="polite"
        className="pointer-events-none fixed inset-x-4 bottom-4 z-[60] flex flex-col items-end gap-2 sm:left-auto"
      >
        {toasts.map((t) => (
          <div
            key={t.id}
            role="status"
            className={cn(
              'pointer-events-auto w-full max-w-sm rounded-lg border px-4 py-3 text-sm shadow-lg',
              tonos[t.tono],
            )}
          >
            <div className="flex items-start justify-between gap-3">
              <span>{t.mensaje}</span>
              <button
                type="button"
                onClick={() => quitar(t.id)}
                aria-label="Descartar"
                className="text-current opacity-60 hover:opacity-100"
              >
                ×
              </button>
            </div>
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
