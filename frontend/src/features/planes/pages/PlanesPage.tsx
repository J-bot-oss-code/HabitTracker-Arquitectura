import { Link } from 'react-router'
import { Alert } from '@/shared/ui/Alert'
import { PageHeader } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Esqueleto } from '@/shared/ui/Spinner'
import { useRecomendados } from '../api'
import { CatalogoPlanes } from '../components/CatalogoPlanes'
import { TarjetaPlan } from '../components/TarjetaPlan'
import { esMiDia } from '../types'

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
              <div className="mt-4 max-w-md">
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
              <ul className={`mt-4 ${REJILLA}`}>
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
              <ul className={`mt-4 ${REJILLA}`}>
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
