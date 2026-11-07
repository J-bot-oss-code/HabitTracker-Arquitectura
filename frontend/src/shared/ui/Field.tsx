import { useEffect, useId, useRef, useState } from 'react'
import { flushSync } from 'react-dom'
import type {
  InputHTMLAttributes,
  ReactNode,
  Ref,
  SelectHTMLAttributes,
  TextareaHTMLAttributes,
} from 'react'
import { cn } from '@/shared/lib/cn'
import { Icono } from './Icono'

const control =
  'block w-full min-h-11 rounded-xl border bg-superficie px-3.5 py-2 text-sm text-texto placeholder:text-atenuado transition-colors disabled:cursor-not-allowed disabled:opacity-60'

function claseControl(error?: string, valido?: boolean) {
  return cn(
    control,
    error
      ? 'border-peligro'
      : valido
        ? 'border-exito focus:border-exito'
        : 'border-control hover:border-acento focus:border-acento',
  )
}

interface CampoBase {
  etiqueta: string
  error?: string
  ayuda?: ReactNode
  // Confirmación en vivo: el valor ya cumple las reglas (borde verde y marca). No sustituye al error.
  valido?: boolean
  // Id de un elemento externo que también describe el campo (p. ej. la lista de requisitos).
  descritoPor?: string
  // La ayuda cambia mientras se escribe (p. ej. "Las contraseñas coinciden"): se anuncia sin
  // interrumpir (aria-live polite).
  ayudaEnVivo?: boolean
}

function Campo({
  id,
  etiqueta,
  error,
  ayuda,
  valido,
  ayudaEnVivo,
  children,
}: Omit<CampoBase, 'descritoPor'> & { id: string; children: ReactNode }) {
  return (
    <div className="space-y-1.5">
      <label htmlFor={id} className="block text-sm font-medium text-texto">
        {etiqueta}
      </label>
      {children}
      {ayuda && !error && (
        <p
          id={`${id}-ayuda`}
          aria-live={ayudaEnVivo ? 'polite' : undefined}
          className={cn('text-xs', valido ? 'font-medium text-exito' : 'text-atenuado')}
        >
          {ayuda}
        </p>
      )}
      {error && (
        <p id={`${id}-error`} role="alert" className="text-xs font-medium text-peligro">
          {error}
        </p>
      )}
    </div>
  )
}

function describedBy(id: string, error?: string, ayuda?: ReactNode, extra?: string) {
  const propio = error ? `${id}-error` : ayuda ? `${id}-ayuda` : undefined
  return [propio, extra].filter(Boolean).join(' ') || undefined
}

function MarcaValido() {
  return (
    <span className="pointer-events-none absolute inset-y-0 right-0 flex w-11 items-center justify-center text-exito">
      <Icono nombre="check" className="size-5" />
    </span>
  )
}

type InputProps = CampoBase &
  InputHTMLAttributes<HTMLInputElement> & { ref?: Ref<HTMLInputElement> }

export function Input({
  etiqueta,
  error,
  ayuda,
  valido,
  descritoPor,
  ayudaEnVivo,
  id,
  className,
  ...props
}: InputProps) {
  const generado = useId()
  const campoId = id ?? generado
  const conMarca = valido && !error
  return (
    <Campo
      id={campoId}
      etiqueta={etiqueta}
      error={error}
      ayuda={ayuda}
      valido={valido && !error}
      ayudaEnVivo={ayudaEnVivo}
    >
      <div className="relative">
        <input
          id={campoId}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy(campoId, error, ayuda, descritoPor)}
          className={cn(claseControl(error, conMarca), conMarca && 'pr-11', className)}
          {...props}
        />
        {conMarca && <MarcaValido />}
      </div>
    </Campo>
  )
}

type PasswordInputProps = Omit<InputProps, 'type'>

// Contraseña con el botón del ojo para verla mientras se escribe. El botón no envía el formulario
// (type="button"), tiene un nombre fijo y anuncia su estado con aria-pressed (botón conmutador),
// y queda justo después del campo al tabular.
export function PasswordInput({
  etiqueta,
  error,
  ayuda,
  valido,
  descritoPor,
  ayudaEnVivo,
  id,
  className,
  ...props
}: PasswordInputProps) {
  const generado = useId()
  const campoId = id ?? generado
  const [visible, setVisible] = useState(false)
  // Contenedor propio para llegar al <form> sin tocar la ref que pasa react-hook-form.
  const contenedor = useRef<HTMLDivElement>(null)

  // Al enviar se vuelve a ocultar ANTES de que el navegador lea el formulario: así no guarda la
  // clave en el autocompletado de texto y sigue ofreciendo el gestor de contraseñas.
  useEffect(() => {
    const formulario = contenedor.current?.querySelector('input')?.form
    if (!formulario) return
    const ocultar = () => flushSync(() => setVisible(false))
    formulario.addEventListener('submit', ocultar, { capture: true })
    return () => formulario.removeEventListener('submit', ocultar, { capture: true })
  }, [])
  return (
    <Campo
      id={campoId}
      etiqueta={etiqueta}
      error={error}
      ayuda={ayuda}
      valido={valido && !error}
      ayudaEnVivo={ayudaEnVivo}
    >
      <div ref={contenedor} className="relative">
        <input
          id={campoId}
          type={visible ? 'text' : 'password'}
          autoCapitalize="none"
          autoCorrect="off"
          spellCheck={false}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy(campoId, error, ayuda, descritoPor)}
          className={cn(claseControl(error, valido && !error), 'pr-12', className)}
          {...props}
        />
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          aria-label="Mostrar contraseña"
          aria-pressed={visible}
          aria-controls={campoId}
          className="absolute inset-y-0 right-0 flex w-11 items-center justify-center rounded-r-xl text-atenuado hover:text-texto"
        >
          <Icono nombre={visible ? 'ojoTachado' : 'ojo'} className="size-5" />
        </button>
      </div>
    </Campo>
  )
}

type SelectProps = CampoBase &
  SelectHTMLAttributes<HTMLSelectElement> & { ref?: Ref<HTMLSelectElement> }

export function Select({
  etiqueta,
  error,
  ayuda,
  valido,
  descritoPor,
  ayudaEnVivo,
  id,
  className,
  children,
  ...props
}: SelectProps) {
  const generado = useId()
  const campoId = id ?? generado
  return (
    <Campo
      id={campoId}
      etiqueta={etiqueta}
      error={error}
      ayuda={ayuda}
      valido={valido && !error}
      ayudaEnVivo={ayudaEnVivo}
    >
      <select
        id={campoId}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(campoId, error, ayuda, descritoPor)}
        className={cn(claseControl(error, valido && !error), className)}
        {...props}
      >
        {children}
      </select>
    </Campo>
  )
}

type TextareaProps = CampoBase &
  TextareaHTMLAttributes<HTMLTextAreaElement> & { ref?: Ref<HTMLTextAreaElement> }

export function Textarea({
  etiqueta,
  error,
  ayuda,
  valido,
  descritoPor,
  ayudaEnVivo,
  id,
  className,
  ...props
}: TextareaProps) {
  const generado = useId()
  const campoId = id ?? generado
  return (
    <Campo
      id={campoId}
      etiqueta={etiqueta}
      error={error}
      ayuda={ayuda}
      valido={valido && !error}
      ayudaEnVivo={ayudaEnVivo}
    >
      <textarea
        id={campoId}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(campoId, error, ayuda, descritoPor)}
        className={cn(claseControl(error, valido && !error), 'min-h-24', className)}
        {...props}
      />
    </Campo>
  )
}
