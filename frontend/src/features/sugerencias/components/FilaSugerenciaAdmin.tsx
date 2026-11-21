import { mensajeDeError } from '@/shared/api/problem'
import { cn } from '@/shared/lib/cn'
import { Button } from '@/shared/ui/Button'
import { useCambiarEstadoSugerencia } from '../api'
import { fechaLocal, idCorto } from '../formato'
import {
  ESTADO_PARA_ADMIN,
  ROTULO_TIPO,
  type EstadoDestino,
  type SugerenciaAdminDto,
} from '../types'
import { BadgeEstadoSugerencia } from './BadgeEstadoSugerencia'
import { TextoSugerencia } from './TextoSugerencia'

// Una sugerencia en la bandeja. El servidor decide qué cambios valen (nada vuelve a «Nueva»; repetir un estado es
// inofensivo): aquí solo se ofrece lo que tiene efecto y siempre se muestra el rechazo del servidor.
export function FilaSugerenciaAdmin({
  sugerencia: s,
  alCambiar,
  alFallar,
}: {
  sugerencia: SugerenciaAdminDto
  // La fila puede salir de la lista (filtro «Nuevas», o un 404 porque ya no existe) antes de terminar: el resultado, bueno
  // o malo, lo dice la página, que también decide a dónde va el foco.
  alCambiar: (id: string, aviso: string) => void
  alFallar: (mensaje: string) => void
}) {
  const cambiar = useCambiarEstadoSugerencia()
  const referencia = `${ROTULO_TIPO[s.tipo]} del ${fechaLocal(s.creadaEn)}`
  const nueva = s.estado === 'Nueva'
  const id = idCorto(s.usuarioId)

  const pulsar = async (estado: EstadoDestino) => {
    // Una resuelta que se reabre queda «leída»: no se dice «marcada», se dice que volvió.
    const aviso =
      s.estado === 'Resuelta'
        ? `«${referencia}»: vuelta a leída.`
        : `«${referencia}» marcada como ${estado === 'Leida' ? 'leída' : 'resuelta'}.`
    try {
      await cambiar.mutateAsync({ id: s.id, estado })
      alCambiar(s.id, aviso)
    } catch (error) {
      alFallar(mensajeDeError(error, 'No se pudo cambiar el estado. Inténtalo de nuevo.'))
    }
  }
  const pendiente = (estado: EstadoDestino) =>
    cambiar.isPending && cambiar.variables?.estado === estado

  return (
    <li
      data-id={s.id}
      tabIndex={-1}
      className={cn(
        'min-w-0 px-4 py-4 outline-none focus-visible:outline-2 focus-visible:-outline-offset-2',
        // Lo nuevo se marca como el ítem activo del menú (barra de acento y fondo suave), no solo con la insignia.
        nueva && 'bg-acento-suave shadow-[inset_3px_0_0_var(--c-acento)]',
      )}
    >
      <p className="flex flex-wrap items-center gap-x-2 gap-y-1">
        <span className={nueva ? 'text-sm font-bold text-texto' : 'rotulo'}>
          {ROTULO_TIPO[s.tipo]}
        </span>
        <BadgeEstadoSugerencia estado={s.estado} rotulo={ESTADO_PARA_ADMIN[s.estado]} />
      </p>
      <p className="mt-1 break-words text-sm text-atenuado">
        <time dateTime={s.creadaEn}>{fechaLocal(s.creadaEn)}</time>
        {' · '}
        <code aria-hidden="true" className="font-mono">
          ID {id}
        </code>
        <span className="sr-only">Identificador de la persona {id}</span>
      </p>
      <TextoSugerencia texto={s.texto} />
      <div className="mt-3 flex flex-wrap gap-2">
        {nueva && (
          <Button
            variante="secundario"
            tamano="sm"
            disabled={cambiar.isPending}
            cargando={pendiente('Leida')}
            aria-label={`Marcar como leída: ${referencia}`}
            onClick={() => pulsar('Leida')}
          >
            Marcar como leída
          </Button>
        )}
        {s.estado === 'Resuelta' ? (
          <Button
            variante="secundario"
            tamano="sm"
            disabled={cambiar.isPending}
            cargando={pendiente('Leida')}
            aria-label={`Volver a leída: ${referencia}`}
            onClick={() => pulsar('Leida')}
          >
            Volver a leída
          </Button>
        ) : (
          <Button
            variante="secundario"
            tamano="sm"
            disabled={cambiar.isPending}
            cargando={pendiente('Resuelta')}
            aria-label={`Marcar como resuelta: ${referencia}`}
            onClick={() => pulsar('Resuelta')}
          >
            Marcar como resuelta
          </Button>
        )}
      </div>
    </li>
  )
}
