import { Link } from 'react-router'
import { Badge } from '@/shared/ui/Badge'
import { Icono } from '@/shared/ui/Icono'
import { etiquetaArea, type MetaCatalogo } from '../catalogo'

// GoalCard de la especificación (§3): área, nivel, nombre, descripción, duración y la acción.
// Elegir un plan pide registro: la meta viaja en la URL y se conserva hasta el onboarding.
export function TarjetaMeta({
  meta,
  nivelTitulo = 3,
}: {
  meta: MetaCatalogo
  // Un nivel por debajo del título de la sección que la contiene.
  nivelTitulo?: 2 | 3
}) {
  const Titulo = nivelTitulo === 2 ? 'h2' : 'h3'
  return (
    <article className="flex w-full flex-col rounded-2xl border border-borde bg-superficie p-5">
      <div className="flex items-center justify-between gap-2">
        <p className="rotulo">{etiquetaArea[meta.area]}</p>
        <Badge tono="neutro">{meta.nivel === 'Todos' ? 'Todos los niveles' : meta.nivel}</Badge>
      </div>
      <Titulo className="mt-3 text-lg font-bold">{meta.nombre}</Titulo>
      <p className="mt-1.5 flex-1 text-sm text-atenuado">{meta.descripcion}</p>
      <p className="mt-4 flex items-center gap-1.5 text-sm font-medium text-texto-2">
        <Icono nombre="calendario" className="size-4 text-atenuado" />
        {meta.duracion} · {meta.frecuencia}
      </p>
      <Link
        to={`/registro?meta=${encodeURIComponent(meta.slug)}`}
        className="mt-4 inline-flex min-h-11 items-center gap-1.5 self-start rounded-lg font-semibold text-acento underline-offset-4 hover:text-acento-fuerte hover:underline"
      >
        Empezar este plan
        {/* El nombre accesible contiene el texto visible (WCAG 2.5.3) más la meta. */}
        <span className="sr-only">: {meta.nombre}</span>
        <Icono nombre="flecha" className="size-4" />
      </Link>
    </article>
  )
}
