import { useId, useState } from 'react'
import { Button } from '@/shared/ui/Button'
import { EmptyState } from '@/shared/ui/Card'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Select } from '@/shared/ui/Field'
import { Segmentado, type OpcionSegmentada } from '@/shared/ui/Segmentado'
import { Esqueleto } from '@/shared/ui/Spinner'
import { usePlanes } from '../api'
import { ETIQUETA_AREA, ETIQUETA_NIVEL_PLURAL } from '../etiquetas'
import { AREAS_PLAN, NIVELES_PLAN, type AreaPlan, type NivelPlan } from '../types'
import { TarjetaPlan } from './TarjetaPlan'

type FiltroNivel = NivelPlan | 'todos'
type FiltroArea = AreaPlan | 'todas'

// Catálogo completo con filtros por nivel y por área. Se pide una sola vez y se filtra aquí: los contadores
// salen de los mismos datos. El título y el texto cambian entre la página pública y la de la sesión.
export function CatalogoPlanes({
  base,
  titulo,
  descripcion,
  nivelTitulo = 2,
  anclaId,
  noRecomendados,
}: {
  base: '/metas' | '/planes'
  titulo: string
  descripcion: string
  nivelTitulo?: 1 | 2
  // La landing enlaza a /#metas.
  anclaId?: string
  // Slugs que el servidor no recomienda a esta persona: se marcan en la tarjeta (solo con sesión).
  noRecomendados?: ReadonlySet<string>
}) {
  const planes = usePlanes()
  const [nivel, setNivel] = useState<FiltroNivel>('todos')
  const [area, setArea] = useState<FiltroArea>('todas')
  const idTitulo = useId()
  const Titulo = nivelTitulo === 1 ? 'h1' : 'h2'

  const todos = planes.data ?? []
  const porArea = todos.filter((p) => area === 'todas' || p.area === area)
  const visibles = porArea.filter((p) => nivel === 'todos' || p.nivel === nivel)
  const opcionesNivel: OpcionSegmentada<FiltroNivel>[] = [
    { valor: 'todos', etiqueta: 'Todos', contador: planes.data ? porArea.length : undefined },
    ...NIVELES_PLAN.map((n) => ({
      valor: n,
      etiqueta: ETIQUETA_NIVEL_PLURAL[n],
      contador: planes.data ? porArea.filter((p) => p.nivel === n).length : undefined,
    })),
  ]
  const areasConPlanes = AREAS_PLAN.filter((a) => todos.some((p) => p.area === a))

  return (
    <section id={anclaId} aria-labelledby={idTitulo} className="scroll-mt-24 py-10 sm:py-14">
      <Titulo id={idTitulo} className="text-3xl font-extrabold sm:text-[2rem]">
        {titulo}
      </Titulo>
      <p className="mt-2 text-texto-2">{descripcion}</p>

      {planes.data && todos.length > 0 && (
        <div className="mt-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="mb-2 text-sm font-semibold text-texto-2">Nivel</p>
            <Segmentado
              etiqueta="Filtrar planes por nivel"
              opciones={opcionesNivel}
              valor={nivel}
              alCambiar={setNivel}
            />
          </div>
          {areasConPlanes.length > 1 && (
            <div className="sm:w-60">
              <Select
                etiqueta="Área"
                value={area}
                onChange={(e) => setArea(e.target.value as FiltroArea)}
              >
                <option value="todas">Todas las áreas</option>
                {areasConPlanes.map((a) => (
                  <option key={a} value={a}>
                    {ETIQUETA_AREA[a]}
                  </option>
                ))}
              </Select>
            </div>
          )}
        </div>
      )}
      {/* Quien usa un lector de pantalla oye cuántos planes quedaron tras filtrar (WCAG 4.1.3). */}
      <p aria-live="polite" className="sr-only">
        {planes.data ? `Mostrando ${visibles.length} de ${todos.length} planes` : ''}
      </p>

      <div className="mt-6">
        {planes.isPending ? (
          <ul
            role="status"
            aria-label="Cargando planes"
            className="grid grid-cols-[repeat(auto-fill,minmax(min(100%,260px),1fr))] gap-4"
          >
            {Array.from({ length: 6 }, (_, i) => (
              <li key={i}>
                <Esqueleto className="h-56 w-full rounded-2xl" />
              </li>
            ))}
          </ul>
        ) : planes.isError ? (
          <ErrorCarga
            error={planes.error}
            porDefecto="No se pudo cargar el catálogo de planes."
            alReintentar={() => planes.refetch()}
          />
        ) : todos.length === 0 ? (
          <EmptyState
            icono="calendario"
            titulo="Aún no hay planes disponibles"
            descripcion="Vuelve a intentarlo en un rato."
          />
        ) : visibles.length === 0 ? (
          <EmptyState
            icono="buscar"
            titulo="No hay planes con ese filtro"
            descripcion="Prueba con otro nivel u otra área."
            accion={
              <Button
                variante="secundario"
                onClick={() => {
                  setNivel('todos')
                  setArea('todas')
                }}
              >
                Quitar los filtros
              </Button>
            }
          />
        ) : (
          <ul className="grid grid-cols-[repeat(auto-fill,minmax(min(100%,260px),1fr))] gap-4">
            {visibles.map((plan) => (
              <li key={plan.slug} className="flex">
                <TarjetaPlan
                  plan={plan}
                  base={base}
                  nivelTitulo={nivelTitulo === 1 ? 2 : 3}
                  noRecomendado={noRecomendados?.has(plan.slug)}
                />
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  )
}
