import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'
import { Icono, type NombreIcono } from './Icono'

// Cifra protagonista con su rótulo, como un casillero del marcador. La barra lateral de
// acento es decorativa y no recorta el foco ni las sombras.
export function Estadistica({
  etiqueta,
  valor,
  detalle,
  icono,
  destacada = false,
}: {
  etiqueta: string
  valor: ReactNode
  detalle?: ReactNode
  icono?: NombreIcono
  destacada?: boolean
}) {
  return (
    <div
      className={cn(
        'relative overflow-hidden rounded-xl border bg-superficie p-3 pl-4 sm:p-4 sm:pl-5',
        destacada ? 'border-acento shadow-brillo' : 'border-borde',
      )}
    >
      <span
        aria-hidden="true"
        className={cn(
          'absolute inset-y-0 left-0 w-1.5',
          destacada ? 'bg-acento' : 'bg-borde-fuerte',
        )}
      />
      <div className="flex items-center justify-between gap-2">
        <p className="rotulo break-words leading-tight">{etiqueta}</p>
        {icono && (
          <Icono nombre={icono} className="hidden size-4 shrink-0 text-acento-2 sm:block" />
        )}
      </div>
      <p className="cifra mt-2 text-3xl sm:text-4xl">{valor}</p>
      {detalle && <p className="mt-1 text-xs text-atenuado">{detalle}</p>}
    </div>
  )
}
