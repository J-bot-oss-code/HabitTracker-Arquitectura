import { cn } from '@/shared/lib/cn'
import { BarraProgreso } from '@/shared/ui/BarraProgreso'
import { Card, CardBody, EmptyState } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useConstanciaReporte } from '../api'
import type { FilaConstanciaDto, RangoReporte } from '../types'

// El título lo manda el servidor (`pregunta`); este es el mismo texto mientras llega.
const PREGUNTA_POR_DEFECTO = '¿Qué tan constante fui en cada plan durante este período?'
// La pregunta del servidor está en primera persona («fui»): solo vale para quien ve lo suyo.
const TITULO_PROMEDIO = 'Constancia promedio por plan'

// Los promedios del Administrador traen un decimal.
const numero = new Intl.NumberFormat('es-DO', { maximumFractionDigits: 1 })

function FilaPlan({ fila: f, promedio }: { fila: FilaConstanciaDto; promedio: boolean }) {
  return (
    <li className="border-t border-borde py-4">
      <div className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1">
        <h3 className="min-w-0 break-words font-semibold">{f.plan}</h3>
        {f.porcentajeConstancia !== null && (
          // Sin culpa: un 0 % es un punto de partida, no una cifra en alarma.
          <p className={cn('cifra text-2xl', f.porcentajeConstancia === 0 && 'text-atenuado')}>
            {numero.format(f.porcentajeConstancia)} %
          </p>
        )}
      </div>
      {f.porcentajeConstancia === null ? (
        <p className="mt-2 text-sm text-atenuado">Sin días en marcha en este período.</p>
      ) : (
        <>
          <BarraProgreso
            porcentaje={f.porcentajeConstancia}
            etiqueta={`Constancia en ${f.plan}`}
            className="mt-2"
          />
          {f.porcentajeConstancia === 0 && (
            <p className="mt-2 text-sm font-medium">Sin avances en este período</p>
          )}
        </>
      )}
      <p className="mt-2 text-sm text-texto-2">
        {promedio && 'En promedio: '}
        {numero.format(f.diasConAvance)} de {numero.format(f.diasEnRango)}{' '}
        {f.diasEnRango === 1 ? 'día' : 'días'} con avance
        {f.diasConAvance > 0 && `; ${numero.format(f.diasSoloMinimo)} solo con la versión mínima`}.
      </p>
      {promedio && (
        <p className="mt-0.5 text-sm text-atenuado">
          Promedio de {f.personas} {f.personas === 1 ? 'persona' : 'personas'}
        </p>
      )}
    </li>
  )
}

// Constancia por plan (RF-REP-02 y RF-NEG-08): de los días en marcha del período, en cuántos hubo avance. La versión
// mínima también cuenta como avance. El Administrador ve promedios por plan del catálogo, nunca datos personales.
export function BloqueConstancia({ rango }: { rango: RangoReporte }) {
  const reporte = useConstanciaReporte(rango)
  const d = reporte.data
  const promedio = d?.alcance === 'todos'

  return (
    <section aria-labelledby="reporte-constancia" aria-busy={reporte.isFetching}>
      <Card>
        <CardBody>
          <h2 id="reporte-constancia" className="text-lg font-bold">
            {!d ? PREGUNTA_POR_DEFECTO : promedio ? TITULO_PROMEDIO : d.pregunta}
          </h2>
          {d && (
            <p className="mt-1 text-sm text-atenuado">
              {promedio
                ? 'Promedios por plan de todas las personas, sin datos personales.'
                : 'Solo tus planes.'}
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
                icono="habitos"
                titulo="Sin planes en marcha en este rango"
                descripcion="Cuando haya un plan en marcha en el período, verás aquí cuántos días avanzaste."
              />
            ) : (
              d && (
                <ul aria-label="Constancia por plan" className="border-b border-borde">
                  {d.filas.map((f) => (
                    <FilaPlan key={f.plan} fila={f} promedio={promedio} />
                  ))}
                </ul>
              )
            )}
          </div>
        </CardBody>
      </Card>
    </section>
  )
}
