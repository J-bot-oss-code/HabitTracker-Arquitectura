import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import { cn } from '@/shared/lib/cn'
import { claseBoton } from '@/shared/ui/claseBoton'
import { EmptyState, PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Segmentado } from '@/shared/ui/Segmentado'
import { CargandoPagina } from '@/shared/ui/Spinner'
import { useContadorNoLeidas, useNotificaciones } from '../api'
import { FilaNotificacion } from '../components/FilaNotificacion'

type Filtro = 'todas' | 'sin-leer'

// Bandeja interna (RF-NOT-04 y 05): los avisos propios, del más reciente al más antiguo. El servidor devuelve solo los
// de quien consulta, sea cual sea su rol (RD-06). La bandeja se conserva aunque se apague el correo (RF-NOT-07).
export function NotificacionesPage() {
  const [filtro, setFiltro] = useState<Filtro>('todas')
  // Un objeto nuevo en cada aviso: el efecto del foco se repite aunque el texto sea el mismo.
  const [aviso, setAviso] = useState<{ texto: string } | null>(null)
  const avisoRef = useRef<HTMLParagraphElement>(null)
  const lista = useNotificaciones(filtro === 'sin-leer')
  const sinLeer = useContadorNoLeidas()

  const cambiarFiltro = (nuevo: Filtro) => {
    setFiltro(nuevo)
    setAviso(null)
  }
  // La fila marcada pierde su botón (y en «Sin leer» desaparece): el foco pasa al aviso para no caer al <body>.
  useEffect(() => {
    if (aviso) avisoRef.current?.focus()
  }, [aviso])
  const alMarcar = (titulo: string) => setAviso({ texto: `«${titulo}» marcada como leída.` })

  return (
    <>
      <PageHeader
        titulo="Notificaciones"
        descripcion="Los avisos de tus solicitudes y de tus logros. Solo tú los ves."
        acciones={
          <Link
            to="/ajustes/notificaciones"
            className={claseBoton({ variante: 'secundario', tamano: 'sm' })}
          >
            Avisos por correo
          </Link>
        }
      />
      <div className="space-y-4">
        <Segmentado<Filtro>
          etiqueta="Filtrar notificaciones"
          valor={filtro}
          alCambiar={cambiarFiltro}
          opciones={[
            { valor: 'todas', etiqueta: 'Todas' },
            { valor: 'sin-leer', etiqueta: 'Sin leer', contador: sinLeer.data },
          ]}
        />
        {aviso && (
          <p
            ref={avisoRef}
            role="status"
            tabIndex={-1}
            className="text-sm text-atenuado outline-none focus-visible:outline-2 focus-visible:outline-offset-2"
          >
            {aviso.texto}
          </p>
        )}

        {lista.isPending ? (
          <CargandoPagina texto="Cargando tus notificaciones…" />
        ) : lista.isError ? (
          <ErrorCarga
            error={lista.error}
            porDefecto="No se pudieron cargar tus notificaciones."
            alReintentar={() => lista.refetch()}
          />
        ) : lista.data.length === 0 ? (
          filtro === 'sin-leer' ? (
            <EmptyState
              icono="check"
              titulo="Estás al día"
              descripcion="No tienes avisos sin leer."
            />
          ) : (
            <EmptyState
              icono="notificaciones"
              titulo="Todavía no tienes avisos"
              descripcion="Aquí aparecerán los avisos de tus solicitudes y tus logros."
            />
          )
        ) : (
          <ul
            aria-label="Tus notificaciones"
            aria-busy={lista.isFetching}
            className={cn('border-b border-borde', lista.isFetching && 'opacity-60')}
          >
            {lista.data.map((n) => (
              <FilaNotificacion key={n.id} notificacion={n} alMarcar={alMarcar} />
            ))}
          </ul>
        )}
      </div>
    </>
  )
}
