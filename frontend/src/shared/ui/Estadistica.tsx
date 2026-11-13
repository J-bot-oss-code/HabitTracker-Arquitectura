import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'
import { BarraProgreso } from './BarraProgreso'
import { Icono, type NombreIcono } from './Icono'

// Cifra de una franja: etiqueta, número grande y detalle, con una barra de progreso opcional. No tiene caja propia: la
// dibuja <FranjaCifras>, que las junta regla a regla. "Destacada" marca un logro con el fondo de acento.
export function Estadistica({
  etiqueta,
  valor,
  detalle,
  icono,
  destacada = false,
  progreso,
}: {
  etiqueta: string
  valor: ReactNode
  detalle?: ReactNode
  icono?: NombreIcono
  destacada?: boolean
  // Porcentaje 0–100 para la barra; se omite si no aplica.
  progreso?: number
}) {
  return (
    <div className={cn('bg-superficie p-4 sm:p-5', destacada && 'bg-acento-suave')}>
      <div className="flex items-center justify-between gap-2">
        <p className="break-words text-sm font-medium leading-tight text-atenuado">{etiqueta}</p>
        {icono && (
          <span className="hidden shrink-0 text-atenuado sm:block">
            <Icono nombre={icono} className="size-4" />
          </span>
        )}
      </div>
      <p className="cifra mt-2 text-[1.75rem] sm:text-3xl">{valor}</p>
      {progreso !== undefined && (
        <BarraProgreso porcentaje={progreso} etiqueta={etiqueta} className="mt-3" />
      )}
      {detalle && <p className="mt-2 text-xs text-atenuado">{detalle}</p>}
    </div>
  )
}

// Varias cifras en una sola franja separada por reglas finas, en lugar de una tarjeta por cifra. Las columnas van en
// `className` (grid-cols-*); con etiqueta es una región con nombre.
export function FranjaCifras({
  etiqueta,
  className,
  children,
}: {
  etiqueta?: string
  className?: string
  children: ReactNode
}) {
  return (
    <section
      aria-label={etiqueta}
      className={cn(
        'mb-6 grid gap-px overflow-hidden rounded-lg border border-borde bg-borde',
        className,
      )}
    >
      {children}
    </section>
  )
}
