import type { ReactNode } from 'react'
import { cn } from '@/shared/lib/cn'
import { Icono, type NombreIcono } from './Icono'

type Tono = 'info' | 'exito' | 'aviso' | 'peligro'

const tonos: Record<Tono, { clase: string; icono: NombreIcono }> = {
  info: { clase: 'border-info/50 bg-info/10', icono: 'info' },
  exito: { clase: 'border-exito/50 bg-exito/10', icono: 'check' },
  aviso: { clase: 'border-aviso/50 bg-aviso/10', icono: 'alerta' },
  peligro: { clase: 'border-peligro/50 bg-peligro/10', icono: 'alerta' },
}

const colorIcono: Record<Tono, string> = {
  info: 'text-info',
  exito: 'text-exito',
  aviso: 'text-aviso',
  peligro: 'text-peligro',
}

export function Alert({
  tono = 'info',
  titulo,
  children,
  className,
  alCerrar,
  etiquetaCerrar,
}: {
  tono?: Tono
  titulo?: string
  children?: ReactNode
  className?: string
  // Con él aparece una X para descartar el aviso (quien lo usa decide si recordarlo).
  alCerrar?: () => void
  etiquetaCerrar?: string
}) {
  return (
    <div
      role={tono === 'peligro' ? 'alert' : 'status'}
      className={cn(
        'flex gap-3 rounded-lg border px-4 py-3 text-sm text-texto',
        tonos[tono].clase,
        className,
      )}
    >
      <Icono
        nombre={tonos[tono].icono}
        className={cn('mt-0.5 size-5 shrink-0', colorIcono[tono])}
      />
      <div className="min-w-0 flex-1">
        {titulo && <p className="font-semibold">{titulo}</p>}
        {children && <div className={titulo ? 'mt-1' : undefined}>{children}</div>}
      </div>
      {alCerrar && (
        <button
          type="button"
          onClick={alCerrar}
          aria-label={etiquetaCerrar ?? (titulo ? `Cerrar aviso: ${titulo}` : 'Cerrar aviso')}
          data-cerrar-aviso
          className="-my-2.5 -mr-2.5 grid size-11 shrink-0 place-items-center rounded-lg text-atenuado hover:bg-hover hover:text-texto"
        >
          <Icono nombre="cerrar" className="size-4" />
        </button>
      )}
    </div>
  )
}
