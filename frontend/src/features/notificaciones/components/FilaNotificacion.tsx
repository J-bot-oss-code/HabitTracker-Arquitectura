import { mensajeDeError } from '@/shared/api/problem'
import { cn } from '@/shared/lib/cn'
import { formatearFechaHora } from '@/shared/lib/fechas'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Icono, type NombreIcono } from '@/shared/ui/Icono'
import { useMarcarLeida } from '../api'
import type { NotificacionDto, TipoNotificacion } from '../types'

// Un logro se presenta como algo hecho (el resaltador); los avisos de solicitudes son neutros. Ninguno usa rojo.
const PRESENTACION: Record<TipoNotificacion, { rotulo: string; icono: NombreIcono; chip: string }> =
  {
    SolicitudCreada: {
      rotulo: 'Solicitud creada',
      icono: 'permisos',
      chip: 'bg-superficie-2 text-acento',
    },
    SolicitudResuelta: {
      rotulo: 'Solicitud resuelta',
      icono: 'check',
      chip: 'bg-superficie-2 text-acento',
    },
    LogroObtenido: {
      rotulo: 'Logro',
      icono: 'trofeo',
      chip: 'bg-marcador text-marcador-texto',
    },
  }

export function FilaNotificacion({
  notificacion: n,
  alMarcar,
}: {
  notificacion: NotificacionDto
  // La fila puede desaparecer de la lista (filtro «Sin leer») antes de que termine: lo avisa la página.
  alMarcar: (titulo: string) => void
}) {
  const marcar = useMarcarLeida()
  const p = PRESENTACION[n.tipo]

  const pulsar = async () => {
    try {
      await marcar.mutateAsync(n.id)
      alMarcar(n.titulo)
    } catch {
      // El rechazo se muestra en la propia fila (marcar.error).
    }
  }

  return (
    <li
      className={cn(
        'flex flex-col gap-3 border-t border-borde px-3 py-4 sm:flex-row sm:items-start sm:gap-4',
        !n.leida && 'bg-superficie',
      )}
    >
      <span
        aria-hidden="true"
        className={cn('grid size-9 shrink-0 place-items-center rounded-md', p.chip)}
      >
        <Icono nombre={p.icono} className="size-5" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <span className="rotulo">{p.rotulo}</span>
          {!n.leida && <Badge tono="marca">Nueva</Badge>}
        </p>
        <p className={cn('mt-1 break-words', n.leida ? 'font-medium' : 'font-bold')}>{n.titulo}</p>
        <p className="mt-0.5 break-words text-sm text-texto-2">{n.mensaje}</p>
        <p className="mt-1 text-sm text-atenuado">
          <time dateTime={n.creadaEnUtc}>{formatearFechaHora(n.creadaEnUtc)}</time>
          {n.leida && ' · Leída'}
        </p>
        {marcar.isError && (
          <Alert tono="peligro" className="mt-3">
            {mensajeDeError(marcar.error, 'No se pudo marcar el aviso como leído.')}
          </Alert>
        )}
      </div>
      {!n.leida && (
        <Button
          variante="secundario"
          tamano="sm"
          cargando={marcar.isPending}
          aria-label={`Marcar como leída: ${n.titulo}`}
          onClick={pulsar}
          className="self-start"
        >
          Marcar como leída
        </Button>
      )}
    </li>
  )
}
