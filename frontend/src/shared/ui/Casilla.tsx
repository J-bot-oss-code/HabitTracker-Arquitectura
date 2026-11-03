import { useId } from 'react'
import type { InputHTMLAttributes, ReactNode, Ref } from 'react'

type CasillaProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> & {
  children: ReactNode
  error?: string
  ref?: Ref<HTMLInputElement>
}

// Casilla de verificación con su texto como etiqueta (todo el renglón es pulsable, 44 px de
// alto). La usan los consentimientos (§3, ConsentFieldset): ninguna llega marcada por defecto.
export function Casilla({ children, error, id, className, ...props }: CasillaProps) {
  const generado = useId()
  const campoId = id ?? generado
  return (
    <div className={className}>
      <label htmlFor={campoId} className="flex min-h-11 cursor-pointer items-start gap-3 py-1">
        <input
          id={campoId}
          type="checkbox"
          aria-invalid={error ? true : undefined}
          aria-describedby={error ? `${campoId}-error` : undefined}
          className="mt-0.5 size-5 shrink-0 cursor-pointer rounded accent-acento"
          {...props}
        />
        <span className="text-sm leading-snug text-texto-2">{children}</span>
      </label>
      {error && (
        <p id={`${campoId}-error`} role="alert" className="ml-8 text-xs font-medium text-peligro">
          {error}
        </p>
      )}
    </div>
  )
}
