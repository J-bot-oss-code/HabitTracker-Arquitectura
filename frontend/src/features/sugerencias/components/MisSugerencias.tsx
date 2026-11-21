import { EmptyState } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useMisSugerencias } from '../api'
import { fechaLocal } from '../formato'
import { ESTADO_PARA_PERSONA, ROTULO_TIPO } from '../types'
import { BadgeEstadoSugerencia } from './BadgeEstadoSugerencia'
import { TextoSugerencia } from './TextoSugerencia'

// «Lo que has enviado»: lo propio, de lo más reciente a lo más antiguo. El estado se dice como lo lee quien escribió
// (Recibida, Leída, Resuelta).
export function MisSugerencias() {
  const mias = useMisSugerencias()

  let contenido
  if (mias.isPending) {
    contenido = (
      <div className="space-y-3">
        <Esqueleto className="h-20" />
        <Esqueleto className="h-20" />
      </div>
    )
  } else if (mias.isError) {
    contenido = (
      <ErrorCarga
        error={mias.error}
        porDefecto="No se pudieron cargar tus mensajes."
        alReintentar={() => mias.refetch()}
      />
    )
  } else if (mias.data.length === 0) {
    contenido = (
      <EmptyState
        icono="info"
        titulo="Todavía no has escrito nada"
        descripcion="Cuando nos cuentes algo, lo verás aquí con su estado."
      />
    )
  } else {
    contenido = (
      <ul
        aria-label="Lo que has enviado, de lo más reciente a lo más antiguo"
        aria-busy={mias.isFetching}
        className="divide-y divide-borde overflow-hidden rounded-2xl border border-borde bg-superficie"
      >
        {mias.data.map((s) => (
          <li key={s.id} className="min-w-0 px-4 py-4">
            <p className="flex flex-wrap items-center gap-x-2 gap-y-1">
              <span className="rotulo">{ROTULO_TIPO[s.tipo]}</span>
              <BadgeEstadoSugerencia estado={s.estado} rotulo={ESTADO_PARA_PERSONA[s.estado]} />
            </p>
            <p className="mt-1 text-sm text-atenuado">
              <time dateTime={s.creadaEn}>{fechaLocal(s.creadaEn)}</time>
            </p>
            <TextoSugerencia texto={s.texto} />
          </li>
        ))}
      </ul>
    )
  }

  return (
    <section aria-labelledby="titulo-enviado" className="space-y-3">
      <h2 id="titulo-enviado" className="text-lg font-bold">
        Lo que has enviado
      </h2>
      <p className="text-sm text-atenuado">
        No respondemos uno por uno, pero cada mensaje se lee y se usa para mejorar.
      </p>
      {contenido}
    </section>
  )
}
