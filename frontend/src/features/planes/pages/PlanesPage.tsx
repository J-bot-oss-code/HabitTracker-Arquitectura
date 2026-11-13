import { Link } from 'react-router'
import { useMisPlanes } from '@/features/mis-planes/api'
import type { PlanUsuarioDto } from '@/features/mis-planes/types'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { BarraProgreso } from '@/shared/ui/BarraProgreso'
import { Card, CardBody, PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useRecomendados } from '../api'
import { CatalogoPlanes } from '../components/CatalogoPlanes'
import { TarjetaPlan } from '../components/TarjetaPlan'
import { esMiDia } from '../types'

const FILAS = 'border-b border-borde'
const REJILLA = 'grid grid-cols-[repeat(auto-fill,minmax(min(100%,260px),1fr))] gap-4'

function CargandoRecomendados() {
  return (
    <div role="status" aria-label="Cargando tus recomendaciones" className={REJILLA}>
      <Esqueleto className="h-56 w-full rounded-2xl" />
      <Esqueleto className="h-56 w-full rounded-2xl" />
      <Esqueleto className="h-56 w-full rounded-2xl" />
    </div>
  )
}

const ESTADO_PLAN = {
  Activo: 'En curso',
  Pausado: 'En pausa',
  Completado: 'Completado',
  Cancelado: 'Cancelado',
}

function TarjetaMiPlan({ plan }: { plan: PlanUsuarioDto }) {
  const { progreso: p } = plan
  return (
    <Card>
      <CardBody className="space-y-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <Link to={`/mis-planes/${plan.id}`} className="font-bold hover:underline">
            {plan.nombre}
          </Link>
          <Badge tono={plan.estado === 'Activo' ? 'exito' : 'neutro'}>
            {ESTADO_PLAN[plan.estado]}
          </Badge>
        </div>
        <p className="text-sm text-atenuado">
          Semana {p.semanaActual} de {p.semanasTotales} · racha {p.racha}{' '}
          {p.unidad === 'Dias' ? 'días' : 'semanas'}
        </p>
        <BarraProgreso
          etiqueta={`Avance de ${plan.nombre}`}
          porcentaje={(p.semanaActual / p.semanasTotales) * 100}
        />
      </CardBody>
    </Card>
  )
}

// Los planes que la persona sigue (en curso o en pausa), arriba de todo.
function MisPlanes() {
  const misPlanes = useMisPlanes()
  const enCurso = misPlanes.data?.filter((p) => p.estado === 'Activo' || p.estado === 'Pausado')
  if (!enCurso || enCurso.length === 0) return null
  return (
    <section aria-labelledby="mis-planes-titulo" className="mb-10">
      <h2 id="mis-planes-titulo" className="text-xl font-bold">
        Mis planes
      </h2>
      <ul className={`mt-4 ${REJILLA}`}>
        {enCurso.map((p) => (
          <li key={p.id}>
            <TarjetaMiPlan plan={p} />
          </li>
        ))}
      </ul>
    </section>
  )
}

// Planes con sesión: primero lo que el servidor recomienda para esta persona (con sus motivos) y lo que no le
// conviene (con la razón), y debajo todo el catálogo. "Mi día de trabajo saludable" va siempre primero (P2).
export function PlanesPage() {
  const recomendaciones = useRecomendados()
  const datos = recomendaciones.data
  const miDia = datos?.recomendados.find((r) => esMiDia(r.plan.slug))
  const otros = datos?.recomendados.filter((r) => r !== miDia) ?? []

  return (
    <>
      <PageHeader
        titulo="Planes"
        descripcion="Elige el que va con tu jornada. Puedes cambiarlo cuando quieras."
      />

      <MisPlanes />

      {recomendaciones.isPending && <CargandoRecomendados />}
      {recomendaciones.isError && (
        <ErrorCarga
          error={recomendaciones.error}
          porDefecto="No se pudieron cargar tus recomendaciones."
          alReintentar={() => recomendaciones.refetch()}
        />
      )}

      {datos && (
        <div className="space-y-10">
          {miDia && (
            <section aria-labelledby="mi-dia-titulo">
              <h2 id="mi-dia-titulo" className="text-xl font-bold">
                Tu día de trabajo saludable
              </h2>
              <p className="mt-1 text-sm text-atenuado">
                Empieza por aquí: son pocos hábitos y no pide datos de salud.
              </p>
              <div className="mt-4 border-b border-borde">
                <TarjetaPlan
                  plan={miDia.plan}
                  base="/planes"
                  motivos={miDia.motivos}
                  advertencias={miDia.advertencias}
                />
              </div>
            </section>
          )}

          {!datos.personalizadoConSalud && (
            <Alert tono="info" titulo="Recomendados por tu jornada">
              <p>
                Para afinar estas recomendaciones puedes responder unas preguntas de salud. Solo las
                usamos para recomendarte planes y puedes retirar el permiso cuando quieras.
              </p>
              <Link
                to="/ajustes/perfil-salud"
                className="mt-2 inline-flex min-h-11 items-center font-semibold text-acento underline"
              >
                Responder las preguntas de salud
              </Link>
            </Alert>
          )}

          {otros.length > 0 && (
            <section aria-labelledby="recomendados-titulo">
              <h2 id="recomendados-titulo" className="text-xl font-bold">
                Recomendados para ti
              </h2>
              <ul className={`mt-4 ${FILAS}`}>
                {otros.map((r) => (
                  <li key={r.plan.slug} className="flex">
                    <TarjetaPlan
                      plan={r.plan}
                      base="/planes"
                      motivos={r.motivos}
                      advertencias={r.advertencias}
                    />
                  </li>
                ))}
              </ul>
            </section>
          )}

          {datos.noAptos.length > 0 && (
            <section aria-labelledby="no-aptos-titulo">
              <h2 id="no-aptos-titulo" className="text-xl font-bold">
                No recomendado para ti
              </h2>
              <p className="mt-1 text-sm text-atenuado">
                Puedes leerlos, pero te sugerimos empezar por otro. Cada uno explica por qué no
                encaja ahora.
              </p>
              <ul className={`mt-4 ${FILAS}`}>
                {datos.noAptos.map((n) => (
                  <li key={n.plan.slug} className="flex">
                    <TarjetaPlan plan={n.plan} base="/planes" razones={n.razones} />
                  </li>
                ))}
              </ul>
            </section>
          )}
        </div>
      )}

      <CatalogoPlanes
        base="/planes"
        titulo="Todos los planes"
        descripcion="El catálogo completo, para explorarlo con calma."
        anclaId="catalogo"
        noRecomendados={new Set(datos?.noAptos.map((n) => n.plan.slug))}
      />
    </>
  )
}
