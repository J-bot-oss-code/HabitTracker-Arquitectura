import { Link } from 'react-router'
import { useColaCorreos } from '@/features/cola-correos/api'
import { usePlanes } from '@/features/planes/api'
import { esNoDisponible } from '@/shared/api/problem'
import { formatearFechaHoraLocal } from '@/shared/lib/fechas'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Estadistica, FranjaCifras } from '@/shared/ui/Estadistica'
import { Icono } from '@/shared/ui/Icono'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useAuditoria, useMetricas } from '../api'
import { TablaAuditoria } from '../components/TablaAuditoria'
import { descargarCsv, nombreCsv } from '../csv'
import { metricasACsv, type MetricasDto } from '../metricas'

const ENLACE =
  'inline-flex min-h-11 items-center gap-1.5 font-semibold text-acento underline-offset-4 hover:underline'

// Una cifra oculta por la cohorte mínima no es un cero ni un fallo: se muestra un guion atenuado (que los lectores de
// pantalla leen como «Sin cifra») y, debajo, por qué.
const CIFRA_OCULTA = (
  <>
    <span aria-hidden="true" className="text-atenuado">
      —
    </span>
    <span className="sr-only">Sin cifra</span>
  </>
)

const avisoPrivacidad = (cohorteMinima: number) =>
  `Oculta hasta que haya al menos ${cohorteMinima} personas, para proteger su anonimato.`

const personas = (n: number) => `${n} ${n === 1 ? 'persona' : 'personas'}`

// Resumen (§5.6): lo primero que ve el Administrador. Son métricas agregadas y anónimas (GET /api/admin/metrics): ninguna
// cifra permite saber qué hace una persona concreta, y las que cuentan personas se ocultan con menos de la cohorte mínima.
export function ResumenAdminPage() {
  const metricas = useMetricas()
  const recientes = useAuditoria({ tamano: 8 })
  const m = metricas.data
  const planes = usePlanes()
  // La cola es de otro módulo: si su consulta falla (403, error) la cifra de fallidos queda sin dato y la tarjeta solo con el enlace.
  const cola = useColaCorreos()
  const fallidos = cola.data ? cola.data.filter((c) => c.estado === 'Fallido').length : null
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
        <div role="status" className="space-y-6">
          <span className="sr-only">Cargando las métricas…</span>
          {[0, 1, 2].map((i) => (
            <Esqueleto key={i} className="h-28" />
          ))}
        </div>
      ) : metricas.isError ? (
        <ErrorCarga
          error={metricas.error}
          porDefecto="No se pudieron cargar las métricas."
          alReintentar={() => metricas.refetch()}
        />
      ) : (
        <CifrasDelProducto
          m={metricas.data}
          nombrePlan={nombrePlan}
          fallidos={fallidos}
          colaConError={cola.isError}
        />
      )}

      <section aria-labelledby="reciente-titulo" className="mt-6">
        <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
          <h2 id="reciente-titulo" className="text-lg font-bold">
            Auditoría reciente
          </h2>
          <Link to="/admin/auditoria" className={ENLACE}>
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
            <Link to="/admin/cola-correos" className={ENLACE}>
              Abrir la cola de correos
              <Icono nombre="flecha" className="size-4" />
            </Link>
          </CardBody>
        </Card>
      </section>
    </>
  )
}

// Las cifras del producto en tres grupos: lo que pide atención primero, luego personas y uso. El título de cada grupo es un h2.
function CifrasDelProducto({
  m,
  nombrePlan,
  fallidos,
  colaConError,
}: {
  m: MetricasDto
  nombrePlan: (slug: string) => string
  // Correos fallidos de la cola; null si todavía no se sabe (carga o error de otro módulo).
  fallidos: number | null
  colaConError: boolean
}) {
  const privacidad = avisoPrivacidad(m.cohorteMinima)
  const maximo = Math.max(1, ...m.planesMasIniciados.map((p) => p.cantidad))

  return (
    <>
      <p className="-mt-2 mb-6 text-sm text-atenuado">
        Generado el {formatearFechaHoraLocal(m.generadoEn)}
      </p>

      <section aria-labelledby="atender-titulo">
        <h2 id="atender-titulo" className="mb-3 text-lg font-bold">
          Para atender
        </h2>
        <FranjaCifras className="grid-cols-1 sm:grid-cols-2">
          <Estadistica
            etiqueta="Quejas y mejoras nuevas"
            icono="info"
            valor={m.sugerenciasNuevas}
            detalle={
              <>
                Mensajes sin leer.
                <span className="mt-2 block">
                  <Link to="/admin/sugerencias" className={ENLACE}>
                    Abrir las quejas y mejoras
                    <Icono nombre="flecha" className="size-4" />
                  </Link>
                </span>
              </>
            }
          />
          <Estadistica
            etiqueta="Correos fallidos"
            icono="correo"
            valor={fallidos === null ? CIFRA_OCULTA : fallidos}
            detalle={
              <>
                {colaConError
                  ? 'No se pudo consultar la cola de correos.'
                  : 'Correos que no se pudieron enviar.'}
                <span className="mt-2 block">
                  <Link to="/admin/cola-correos" className={ENLACE}>
                    Revisar los correos fallidos
                    <Icono nombre="flecha" className="size-4" />
                  </Link>
                </span>
              </>
            }
          />
        </FranjaCifras>
      </section>

      <section aria-labelledby="personas-titulo">
        <h2 id="personas-titulo" className="mb-3 text-lg font-bold">
          Personas
        </h2>
        <FranjaCifras className="grid-cols-1 sm:grid-cols-2 lg:grid-cols-4">
          <Estadistica
            etiqueta="Cuentas nuevas"
            icono="cuenta"
            valor={m.cuentasNuevas7d}
            detalle="Creadas en los últimos 7 días."
          />
          <Estadistica
            etiqueta="Personas activas"
            icono="usuarios"
            valor={m.usuariosActivos30d === null ? CIFRA_OCULTA : m.usuariosActivos30d}
            detalle={
              m.usuariosActivos30d === null
                ? privacidad
                : 'Personas distintas con algún avance registrado en 30 días.'
            }
          />
          <Estadistica
            etiqueta="Retención a 30 días"
            icono="rayo"
            valor={m.retencion30d === null ? CIFRA_OCULTA : `${m.retencion30d} %`}
            detalle={
              m.retencion30d === null
                ? `Hay menos de ${m.cohorteMinima} personas que registraron algo hace 30 a 60 días.`
                : 'De quienes registraron algo hace 30 a 60 días, el porcentaje que también lo hizo en los últimos 30.'
            }
          />
          <Estadistica
            etiqueta="Bajas en curso"
            icono="candado"
            valor={m.eliminacionesEnCurso}
            detalle="Cuentas que pidieron eliminarse y siguen en el periodo de gracia."
          />
        </FranjaCifras>
      </section>

      <section aria-labelledby="uso-titulo">
        <h2 id="uso-titulo" className="mb-3 text-lg font-bold">
          Uso
        </h2>
        <FranjaCifras className="grid-cols-1 sm:grid-cols-3">
          <Estadistica
            etiqueta="Registros por correo"
            icono="correo"
            valor={m.registrosPorCorreo7d}
            detalle="Avances registrados con «Ya lo hice» del correo en 7 días."
          />
          <div className="bg-superficie p-4 sm:col-span-2 sm:p-5">
            <h3 className="text-sm font-medium leading-tight text-atenuado">
              Planes más iniciados (desde el inicio)
            </h3>
            {m.planesMasIniciados.length === 0 ? (
              <p className="mt-2 text-sm text-atenuado">
                Ningún plan llega todavía a {m.cohorteMinima} personas, así que no se muestra
                ninguno.
              </p>
            ) : (
              <ul className="mt-3 space-y-3">
                {m.planesMasIniciados.map((p) => (
                  <li key={p.slug}>
                    <div className="mb-1 flex items-baseline justify-between gap-3 text-sm">
                      <span className="min-w-0 break-words font-semibold">
                        {nombrePlan(p.slug)}
                      </span>
                      <span className="shrink-0 tabular-nums text-atenuado">
                        {personas(p.cantidad)}
                      </span>
                    </div>
                    {/* Solo decorativa: no es un avance, y el texto de la fila ya dice cuántas personas. */}
                    <div
                      aria-hidden="true"
                      className="h-2 w-full overflow-hidden rounded-full bg-borde"
                    >
                      <div
                        className="h-full rounded-full bg-acento"
                        style={{ width: `${(p.cantidad / maximo) * 100}%` }}
                      />
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </FranjaCifras>
      </section>
    </>
  )
}
