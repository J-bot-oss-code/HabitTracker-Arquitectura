import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'
import { Icono, type NombreIcono } from './Icono'

export type Tono = 'neutro' | 'info' | 'exito' | 'aviso' | 'peligro' | 'marca'

// El color nunca es la única señal: cada badge puede llevar un icono y siempre lleva texto.
const tonos: Record<Tono, string> = {
  neutro: 'bg-superficie-2 text-texto border-borde-fuerte',
  info: 'bg-info/12 text-info border-info/40',
  exito: 'bg-exito-fondo text-exito border-exito/30',
  // Par sólido probado en contraste.test.ts: el ámbar con alfa no llega a 4,5:1 en Claro.
  aviso: 'bg-nota-fondo text-nota-texto border-nota-borde',
  peligro: 'bg-peligro/12 text-peligro border-peligro/40',
  marca: 'bg-acento-suave text-acento-fuerte border-acento-tinte',
}

export function Badge({
  tono = 'neutro',
  icono,
  children,
}: {
  tono?: Tono
  icono?: NombreIcono
  children: ReactNode
}) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full border px-2.5 py-0.5 text-xs font-semibold',
        tonos[tono],
      )}
    >
      {icono && <Icono nombre={icono} className="size-3.5" />}
      {children}
    </span>
  )
}
