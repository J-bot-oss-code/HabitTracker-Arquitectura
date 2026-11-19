import { cn } from '@/shared/lib/cn'
import { Card, CardBody, EmptyState } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Esqueleto } from '@/shared/ui/Spinner'
import { usePermisosReporte } from '../api'
import type { RangoReporte } from '../types'

// Solicitudes de permiso por estado (RF-REP-01). El alcance lo decide el servidor según el rol (RF-REP-04): aquí solo se
// explica de quién son las cifras.
export function BloquePermisos({ rango }: { rango: RangoReporte }) {
  const reporte = usePermisosReporte(rango)
  const d = reporte.data

  return (
    <section aria-labelledby="reporte-permisos" aria-busy={reporte.isFetching}>
      <Card>
        <CardBody>
          <h2 id="reporte-permisos" className="text-lg font-bold">
            Solicitudes de permiso por estado
          </h2>
          {d && (
            <p className="mt-1 text-sm text-atenuado">
              {d.alcance === 'todos'
                ? 'Las solicitudes de todas las personas.'
                : 'Solo tus solicitudes.'}
            </p>
          )}
          <div className={cn('mt-4', reporte.isFetching && !reporte.isPending && 'opacity-60')}>
            {reporte.isPending ? (
              <>
                <p role="status" className="sr-only">
                  Cargando…
                </p>
                <Esqueleto className="h-32" />
              </>
            ) : reporte.isError ? (
              <ErrorCarga
                error={reporte.error}
                porDefecto="No se pudo cargar este reporte."
                alReintentar={() => reporte.refetch()}
              />
            ) : d && d.filas.length === 0 ? (
              <EmptyState
                icono="permisos"
                titulo="Sin solicitudes en este rango"
                descripcion="Cuando haya solicitudes en el período elegido, aparecerán aquí."
              />
            ) : (
              d && (
                <table className="w-full text-left text-sm">
                  <caption className="sr-only">Solicitudes de permiso por estado</caption>
                  <thead>
                    <tr className="border-b border-borde">
                      <th scope="col" className="py-2 pr-4 rotulo">
                        Estado
                      </th>
                      <th scope="col" className="py-2 text-right rotulo">
                        Cantidad
                      </th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-borde">
                    {d.filas.map((f) => (
                      <tr key={f.estado}>
                        <th scope="row" className="py-2.5 pr-4 font-medium">
                          {f.estado}
                        </th>
                        <td className="cifra py-2.5 text-right text-lg">{f.cantidad}</td>
                      </tr>
                    ))}
                  </tbody>
                  <tfoot>
                    <tr className="border-t border-borde-fuerte">
                      <th scope="row" className="py-2.5 pr-4 font-bold">
                        Total
                      </th>
                      <td className="cifra py-2.5 text-right text-lg">{d.total}</td>
                    </tr>
                  </tfoot>
                </table>
              )
            )}
          </div>
        </CardBody>
      </Card>
    </section>
  )
}
