import type { ReactNode } from 'react'
import { Link } from 'react-router'
import { formatearFechaHora } from '@/shared/lib/fechas'
import { EmptyState } from '@/shared/ui/Card'
import { nombrePermiso } from '../catalogo'
import type { SolicitudPermisoDto } from '../types'
import { EstadoSolicitudBadge } from './EstadoSolicitudBadge'

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
  if (solicitudes.length === 0) return <EmptyState titulo={vacio} />

  const ordenadas = [...solicitudes].sort((a, b) => b.fechaCreacion.localeCompare(a.fechaCreacion))

  return (
    <div className="overflow-x-auto rounded-xl border border-slate-200 bg-white dark:border-slate-800 dark:bg-slate-900">
      <table className="w-full min-w-[640px] text-left text-sm">
        <thead className="border-b border-slate-200 text-xs uppercase tracking-wide text-slate-500 dark:border-slate-800">
          <tr>
            <th scope="col" className="px-4 py-3 font-medium">
              Permiso
            </th>
            {mostrarSolicitante && (
              <th scope="col" className="px-4 py-3 font-medium">
                Solicitante
              </th>
            )}
            <th scope="col" className="px-4 py-3 font-medium">
              Estado
            </th>
            <th scope="col" className="px-4 py-3 font-medium">
              Creada
            </th>
            {acciones && (
              <th scope="col" className="px-4 py-3 font-medium">
                <span className="sr-only">Acciones</span>
              </th>
            )}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
          {ordenadas.map((s) => (
            <tr key={s.id}>
              <td className="px-4 py-3">
                <Link to={`/permisos/${s.id}`} className="font-medium hover:text-brand-600">
                  {nombrePermiso(s.permisoSolicitado)}
                </Link>
                <div className="text-xs text-slate-500">{s.permisoSolicitado}</div>
              </td>
              {mostrarSolicitante && <td className="px-4 py-3">{s.solicitante}</td>}
              <td className="px-4 py-3">
                <EstadoSolicitudBadge estado={s.estado} />
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-slate-500">
                {formatearFechaHora(s.fechaCreacion)}
              </td>
              {acciones && <td className="px-4 py-3 text-right">{acciones(s)}</td>}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
