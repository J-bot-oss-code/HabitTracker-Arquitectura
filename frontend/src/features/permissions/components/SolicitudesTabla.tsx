import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { formatearFechaHora } from '@/shared/lib/fechas'
import { EmptyState } from '@/shared/ui/Card'
import { nombrePermiso } from '../catalogo'
import type { SolicitudPermisoDto } from '../types'
import { EstadoSolicitudBadge } from './EstadoSolicitudBadge'

// Una sola estructura en el DOM: tabla desde `md` y tarjetas por fila en móvil (cada celda
// lleva su rótulo con data-etiqueta). Los roles explícitos conservan la semántica de tabla
// aunque el CSS cambie el `display`.
const celda = 'block px-4 py-1.5 md:table-cell md:py-3'
const conRotulo =
  'before:mb-0.5 before:block before:text-xs before:font-semibold before:uppercase before:tracking-wider before:text-atenuado before:content-[attr(data-etiqueta)] md:before:hidden'
const encabezado = 'px-4 py-3 text-xs font-semibold uppercase tracking-wider text-atenuado'

export function SolicitudesTabla({
  solicitudes,
  mostrarSolicitante = false,
  acciones,
  vacio = 'No hay solicitudes.',
}: {
  solicitudes: SolicitudPermisoDto[]
  mostrarSolicitante?: boolean
  acciones?: (s: SolicitudPermisoDto) => ReactNode
  vacio?: string
}) {
  if (solicitudes.length === 0) return <EmptyState titulo={vacio} icono="permisos" />

  const ordenadas = [...solicitudes].sort((a, b) => b.fechaCreacion.localeCompare(a.fechaCreacion))

  return (
    <div className="md:overflow-hidden md:rounded-xl md:border md:border-borde md:bg-superficie md:shadow-tarjeta">
      <table role="table" className="block w-full text-left text-sm md:table">
        <thead role="rowgroup" className="sr-only md:not-sr-only md:table-header-group">
          <tr role="row" className="md:table-row">
            <th scope="col" className={encabezado}>
              Permiso
            </th>
            {mostrarSolicitante && (
              <th scope="col" className={encabezado}>
                Solicitante
              </th>
            )}
            <th scope="col" className={encabezado}>
              Estado
            </th>
            <th scope="col" className={encabezado}>
              Creada
            </th>
            {acciones && (
              <th scope="col" className="px-4 py-3">
                <span className="sr-only">Acciones</span>
              </th>
            )}
          </tr>
        </thead>
        <tbody
          role="rowgroup"
          className="block space-y-3 md:table-row-group md:space-y-0 md:divide-y md:divide-borde"
        >
          {ordenadas.map((s) => (
            <tr
              key={s.id}
              role="row"
              className="block rounded-xl border border-borde bg-superficie py-3 shadow-tarjeta md:table-row md:rounded-none md:border-0 md:bg-transparent md:py-0 md:shadow-none"
            >
              <td role="cell" data-etiqueta="Permiso" className={`${celda} ${conRotulo}`}>
                <Link to={`/permisos/${s.id}`} className="font-semibold hover:text-acento">
                  {nombrePermiso(s.permisoSolicitado)}
                </Link>
                {/* La clave técnica solo se muestra a quien administra (vista con solicitante). */}
                {mostrarSolicitante && (
                  <div className="text-xs text-atenuado">{s.permisoSolicitado}</div>
                )}
              </td>
              {mostrarSolicitante && (
                <td role="cell" data-etiqueta="Solicitante" className={`${celda} ${conRotulo}`}>
                  {s.solicitante}
                </td>
              )}
              <td role="cell" data-etiqueta="Estado" className={`${celda} ${conRotulo}`}>
                <EstadoSolicitudBadge estado={s.estado} />
              </td>
              <td
                role="cell"
                data-etiqueta="Creada"
                className={`${celda} ${conRotulo} text-atenuado md:whitespace-nowrap`}
              >
                {formatearFechaHora(s.fechaCreacion)}
              </td>
              {acciones && (
                <td role="cell" className={`${celda} md:text-right`}>
                  {acciones(s)}
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
