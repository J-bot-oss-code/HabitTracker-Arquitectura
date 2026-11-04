import type { ButtonHTMLAttributes } from 'react'
import { cn } from '@/shared/lib/cn'
import { claseBoton, type TamanoBoton, type VarianteBoton } from './claseBoton'
import { Spinner } from './Spinner'

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: VarianteBoton
  tamano?: TamanoBoton
  cargando?: boolean
}

export function Button({
  variante = 'primario',
  tamano = 'md',
  cargando = false,
  disabled,
  className,
  children,
  type = 'button',
  ...props
}: ButtonProps) {
  return (
    <button
      type={type}
      disabled={disabled || cargando}
      aria-busy={cargando || undefined}
      className={claseBoton({
        variante,
        tamano,
        className: cn('disabled:cursor-not-allowed', className),
      })}
      {...props}
    >
      {cargando && <Spinner className="size-4" />}
      {children}
    </button>
  )
}
