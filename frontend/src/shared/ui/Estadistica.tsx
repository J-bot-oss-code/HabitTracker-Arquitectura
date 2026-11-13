import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'
import { BarraProgreso } from './BarraProgreso'
import { Icono, type NombreIcono } from './Icono'

// StatCard de la especificación (§3): etiqueta, cifra grande y detalle, con una barra de
// progreso opcional. "Destacada" marca un logro con borde de acento, no con sombras.
export function Estadistica({
  etiqueta,
  valor,
  detalle,
  icono,
  destacada = false,
  progreso,
  plana = false,
}: {
  etiqueta: string
  valor: ReactNode
  detalle?: ReactNode
  icono?: NombreIcono
  destacada?: boolean
  // Porcentaje 0–100 para la barra; se omite si no aplica.
  progreso?: number
  // Sin borde ni esquinas: la celda de una franja de cifras que dibuja el contenedor.
  plana?: boolean
}) {
  return (
    <div
      className={cn(
        'bg-superficie p-4 sm:p-5',
        plana
          ? destacada && 'bg-acento-suave'
          : cn(
              'rounded-2xl border',
              destacada ? 'border-acento ring-1 ring-acento' : 'border-borde',
            ),
      )}
    >
      <div className="flex items-center justify-between gap-2">
        <p className="break-words text-sm font-medium leading-tight text-atenuado">{etiqueta}</p>
        {icono && (
          <span className="hidden size-7 shrink-0 place-items-center rounded-lg bg-acento-suave text-acento-fuerte sm:grid">
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
