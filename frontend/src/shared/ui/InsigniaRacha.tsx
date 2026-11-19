import { cn } from '@/shared/lib/cn'
import { Icono } from './Icono'

// StreakBadge de la especificación (§3): píldora ámbar "12 días de racha". El texto completo
// siempre acompaña al icono, así que no depende del color.
export function InsigniaRacha({ dias, className }: { dias: number; className?: string }) {
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-md bg-racha-fondo px-3 py-1 text-sm font-semibold text-racha-texto',
        className,
      )}
    >
      <Icono nombre="llama" className="size-4" />
      {dias === 1 ? '1 día de racha' : `${dias} días de racha`}
    </span>
  )
}
