import type { ButtonHTMLAttributes } from 'react'
import { cn } from '@/shared/lib/cn'
import { Spinner } from './Spinner'

type Variante = 'primario' | 'secundario' | 'peligro' | 'fantasma'
type Tamano = 'sm' | 'md'

const variantes: Record<Variante, string> = {
  // Deshabilitado: se ve plano y apagado (sin acento ni sombra), no solo más tenue.
  primario:
    'bg-acento text-acento-texto font-semibold shadow-tarjeta hover:brightness-110 active:brightness-95 disabled:border disabled:border-borde disabled:bg-superficie-2 disabled:text-atenuado disabled:shadow-none disabled:brightness-100',
  secundario:
    'border border-borde-fuerte bg-superficie text-texto hover:border-acento hover:bg-superficie-2 disabled:border-borde disabled:text-atenuado disabled:hover:bg-superficie',
  peligro:
    'bg-peligro text-fondo font-semibold hover:brightness-110 active:brightness-95 disabled:border disabled:border-borde disabled:bg-superficie-2 disabled:text-atenuado disabled:brightness-100',
  fantasma: 'text-texto hover:bg-superficie-2 disabled:text-atenuado',
}

// Objetivo táctil de al menos 44 px en ambos tamaños (WCAG 2.2, 2.5.8).
const tamanos: Record<Tamano, string> = {
  sm: 'min-h-11 px-3.5 text-sm',
  md: 'min-h-11 px-5 text-sm',
}

interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variante?: Variante
  tamano?: Tamano
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
      className={cn(
        'inline-flex items-center justify-center gap-2 rounded-lg transition-[filter,background-color,border-color] duration-150 disabled:cursor-not-allowed',
        variantes[variante],
        tamanos[tamano],
        className,
      )}
      {...props}
    >
      {cargando && <Spinner className="size-4" />}
      {children}
    </button>
  )
}
