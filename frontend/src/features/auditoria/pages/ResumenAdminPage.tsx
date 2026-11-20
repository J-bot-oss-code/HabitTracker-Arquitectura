import { Link } from 'react-router'
import { useColaCorreos } from '@/features/cola-correos/api'
import { usePlanes } from '@/features/planes/api'
import { esNoDisponible } from '@/shared/api/problem'
import { BarraProgreso } from '@/shared/ui/BarraProgreso'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, EmptyState, PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Estadistica, FranjaCifras } from '@/shared/ui/Estadistica'
import { Icono } from '@/shared/ui/Icono'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useAuditoria, useMetricas } from '../api'
import { TablaAuditoria } from '../components/TablaAuditoria'
import { descargarCsv, nombreCsv } from '../csv'
import { metricasACsv } from '../metricas'

// Resumen (§5.6): lo primero que ve el Administrador. Son métricas agregadas y anónimas: ninguna
// cifra permite saber qué hace una persona concreta.
export function ResumenAdminPage() {
  const metricas = useMetricas()
  const recientes = useAuditoria({ tamano: 8 })
  const m = metricas.data
  const planes = usePlanes()
  // La cola es de otro módulo: si su consulta falla (403, error) la tarjeta se queda solo con el enlace.
  const cola = useColaCorreos()
  const fallidos = cola.data?.filter((c) => c.estado === 'Fallido').length ?? 0
  const pendientes = cola.data?.filter((c) => c.estado === 'Pendiente').length ?? 0
  // Un plan retirado del catálogo (o con el catálogo sin cargar) se muestra por su slug.
  const nombrePlan = (slug: string) => planes.data?.find((p) => p.slug === slug)?.nombre ?? slug

  return (
    <>
      <PageHeader
        titulo="Resumen"
        descripcion="Métricas agregadas y anónimas."
        acciones={
          <Button
            variante="secundario"
            disabled={!m}
            onClick={() => m && descargarCsv(nombreCsv('metricas'), metricasACsv(m, nombrePlan))}
          >
            <Icono nombre="documentos" className="size-4" />
            Exportar CSV
          </Button>
        }
      />

      {metricas.isPending ? (
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {[0, 1, 2, 3].map((i) => (
            <Esqueleto key={i} className="h-28" />
          ))}
        </div>
      ) : metricas.isError ? (
        esNoDisponible(metricas.error) ? (
          <EmptyState
            icono="calendario"
            titulo="Las métricas llegarán pronto"
            descripcion="Esta parte aún no está disponible en el servidor."
          />
        ) : (
          <ErrorCarga
            error={metricas.error}
            porDefecto="No se pudieron cargar las métricas."
            alReintentar={() => metricas.refetch()}
          />
        )
      ) : (
        m && (
          <>
            <FranjaCifras etiqueta="Indicadores" className="grid-cols-2 lg:grid-cols-4">
              <Estadistica
                etiqueta="Usuarios activos"
                icono="usuarios"
                valor={m.usuariosActivos30d}
                detalle="con sesión en 30 días"
              />
              <Estadistica
                etiqueta="Registros"
                icono="cuenta"
                valor={m.registros7d}
                detalle="últimos 7 días"
              />
              <Estadistica
                etiqueta="Retención a 30 días"
                icono="rayo"
                valor={m.retencion30d === null ? '—' : `${m.retencion30d} %`}
                detalle="de quienes se registraron antes"
              />
              <Estadistica
                etiqueta="Eliminaciones en curso"
                icono="candado"
                valor={m.eliminacionesEnCurso}
                detalle="en periodo de gracia"
              />
            </FranjaCifras>

            <section aria-labelledby="planes-titulo" className="mt-6">
              <Card>
                <CardBody>
                  <h2 id="planes-titulo" className="mb-4 text-lg font-bold">
                    Planes más iniciados
                  </h2>
                  {m.planesMasIniciados.length === 0 ? (
                    <p className="text-sm text-atenuado">Aún nadie ha elegido un plan.</p>
                  ) : (
                    <ul className="space-y-4">
                      {m.planesMasIniciados.map((p) => (
                        <li key={p.slug}>
                          <div className="mb-1 flex items-baseline justify-between gap-3 text-sm">
                            <span className="font-semibold">{nombrePlan(p.slug)}</span>
                            <span className="tabular-nums text-atenuado">
                              {p.cantidad} {p.cantidad === 1 ? 'inicio' : 'inicios'}
                            </span>
                          </div>
                          <BarraProgreso
                            etiqueta={`${nombrePlan(p.slug)}: ${p.cantidad}`}
                            porcentaje={(p.cantidad / m.planesMasIniciados[0].cantidad) * 100}
                          />
                        </li>
                      ))}
                    </ul>
                  )}
                </CardBody>
              </Card>
            </section>
          </>
        )
      )}

      <section aria-labelledby="reciente-titulo" className="mt-6">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h2 id="reciente-titulo" className="text-lg font-bold">
            Auditoría reciente
          </h2>
          <Link
            to="/admin/auditoria"
            className="inline-flex min-h-11 items-center gap-1.5 font-semibold text-acento underline-offset-4 hover:underline"
          >
            Ver toda la auditoría
            <Icono nombre="flecha" className="size-4" />
          </Link>
        </div>
        {recientes.isPending ? (
          <Esqueleto className="h-48" />
        ) : recientes.isError ? (
          esNoDisponible(recientes.error) ? (
            <p className="text-sm text-atenuado">La auditoría llegará pronto.</p>
          ) : (
            <ErrorCarga
              error={recientes.error}
              porDefecto="No se pudo cargar la auditoría."
              alReintentar={() => recientes.refetch()}
            />
          )
        ) : recientes.data.registros.length === 0 ? (
          <p className="text-sm text-atenuado">Todavía no hay eventos.</p>
        ) : (
          <TablaAuditoria eventos={recientes.data.registros} conDetalle={false} />
        )}
      </section>

      <section aria-labelledby="cola-titulo" className="mt-6">
        <Card>
          <CardBody className="flex flex-wrap items-center justify-between gap-3">
            <div>
              <h2 id="cola-titulo" className="text-lg font-bold">
                Cola de correos
              </h2>
              <p className="mt-1 text-sm text-atenuado">
                Revisa los correos enviados, los reintentos y los fallidos.
              </p>
              {cola.data && (
                <p className="mt-2 font-semibold">
                  {fallidos === 0
                    ? 'Todo en orden'
                    : `${fallidos} ${fallidos === 1 ? 'fallido' : 'fallidos'} · ${pendientes} ${pendientes === 1 ? 'pendiente' : 'pendientes'}`}
                </p>
              )}
            </div>
            <Link
              to="/admin/cola-correos"
              className="inline-flex min-h-11 items-center gap-1.5 font-semibold text-acento underline-offset-4 hover:underline"
            >
              Abrir la cola de correos
              <Icono nombre="flecha" className="size-4" />
            </Link>
          </CardBody>
        </Card>
      </section>
    </>
  )
}
