import { useId } from 'react'
import type {
  InputHTMLAttributes,
  ReactNode,
  Ref,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
} from 'react'
import { cn } from '@/shared/lib/cn'

const control =
  'block w-full rounded-lg border bg-white px-3 py-2 text-sm text-slate-900 shadow-xs placeholder:text-slate-400 disabled:bg-slate-100 dark:bg-slate-900 dark:text-slate-100 dark:disabled:bg-slate-800'

function claseControl(error?: string) {
  return cn(
    control,
    error
      ? 'border-rose-500 focus:border-rose-500'
      : 'border-slate-300 focus:border-brand-500 dark:border-slate-700',
  )
}

interface CampoBase {
  etiqueta: string
  error?: string
  ayuda?: ReactNode
}

function Campo({
  id,
  etiqueta,
  error,
  ayuda,
  children,
}: CampoBase & { id: string; children: ReactNode }) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm font-medium text-slate-700 dark:text-slate-300">
        {etiqueta}
      </label>
      {children}
      {ayuda && !error && (
        <p id={`${id}-ayuda`} className="text-xs text-slate-500">
          {ayuda}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs text-rose-600 dark:text-rose-400">
          {error}
        </p>
      )}
    </div>
  )
}

function describedBy(id: string, error?: string, ayuda?: ReactNode) {
  if (error) return `${id}-error`
  if (ayuda) return `${id}-ayuda`
  return undefined
}

type InputProps = CampoBase &
  InputHTMLAttributes<HTMLInputElement> & { ref?: Ref<HTMLInputElement> }

export function Input({ etiqueta, error, ayuda, id, className, ...props }: InputProps) {
  const generado = useId()
  const campoId = id ?? generado
  return (
    <Campo id={campoId} etiqueta={etiqueta} error={error} ayuda={ayuda}>
      <input
        id={campoId}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(campoId, error, ayuda)}
        className={cn(claseControl(error), className)}
        {...props}
      />
    </Campo>
  )
}

type SelectProps = CampoBase &
  SelectHTMLAttributes<HTMLSelectElement> & { ref?: Ref<HTMLSelectElement> }

export function Select({ etiqueta, error, ayuda, id, className, children, ...props }: SelectProps) {
  const generado = useId()
  const campoId = id ?? generado
  return (
    <Campo id={campoId} etiqueta={etiqueta} error={error} ayuda={ayuda}>
      <select
        id={campoId}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(campoId, error, ayuda)}
        className={cn(claseControl(error), className)}
        {...props}
      >
        {children}
      </select>
    </Campo>
  )
}

type TextareaProps = CampoBase &
  TextareaHTMLAttributes<HTMLTextAreaElement> & { ref?: Ref<HTMLTextAreaElement> }

export function Textarea({ etiqueta, error, ayuda, id, className, ...props }: TextareaProps) {
  const generado = useId()
  const campoId = id ?? generado
  return (
    <Campo id={campoId} etiqueta={etiqueta} error={error} ayuda={ayuda}>
      <textarea
        id={campoId}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(campoId, error, ayuda)}
        className={cn(claseControl(error), 'min-h-24', className)}
        {...props}
      />
    </Campo>
  )
}
