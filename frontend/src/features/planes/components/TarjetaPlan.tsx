import { Link } from 'react-router'
import { Badge } from '@/shared/ui/Badge'
import { Icono } from '@/shared/ui/Icono'
import { ETIQUETA_AREA, ETIQUETA_INTENSIDAD, ETIQUETA_NIVEL, duracionPlan } from '../etiquetas'
import type { PlanResumenDto, RazonNoAptoDto } from '../types'

// Tarjeta de un plan (GoalCard de la especificación, §3): la usan el catálogo público, los recomendados y los
// "No recomendado para ti". Nada de lo que muestra depende solo del color.
export function TarjetaPlan({
  plan,
  base,
  nivelTitulo = 3,
  motivos = [],
  advertencias = [],
  razones = [],
  noRecomendado = false,
}: {
  plan: PlanResumenDto
  // /metas (público) o /planes (con sesión): a dónde lleva "Ver el plan".
  base: '/metas' | '/planes'
  // Un nivel por debajo del título de la sección que la contiene.
  nivelTitulo?: 2 | 3
  motivos?: string[]
  advertencias?: string[]
  razones?: RazonNoAptoDto[]
  // En el catálogo completo: el plan también está en «No recomendado para ti» y se marca aquí.
  noRecomendado?: boolean
}) {
  const Titulo = nivelTitulo === 2 ? 'h2' : 'h3'
  return (
    <article className="flex w-full flex-col rounded-2xl border border-borde bg-superficie p-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <p className="rotulo">{ETIQUETA_AREA[plan.area]}</p>
        <Badge tono="neutro">{ETIQUETA_NIVEL[plan.nivel]}</Badge>
      </div>
      {noRecomendado && (
        <p className="mt-2">
          <Badge tono="info" icono="info">
            No recomendado para ti
          </Badge>
        </p>
      )}
      <Titulo className="mt-3 text-lg font-bold">{plan.nombre}</Titulo>
      <p className="mt-1.5 text-sm text-atenuado">{plan.resumen}</p>

      {razones.length > 0 && (
        <ul className="mt-3 space-y-1.5 text-sm text-texto-2">
          {razones.map((r) => (
            <li key={r.motivo} className="flex items-start gap-2">
              <Icono nombre="info" className="mt-0.5 size-4 shrink-0 text-atenuado" />
              <span>{r.mensaje}</span>
            </li>
          ))}
        </ul>
      )}
      {motivos.length > 0 && (
        <ul className="mt-3 space-y-1.5 text-sm text-texto-2">
          {motivos.map((m) => (
            <li key={m} className="flex items-start gap-2">
              <Icono nombre="check" className="mt-0.5 size-4 shrink-0 text-acento" />
              <span>{m}</span>
            </li>
          ))}
        </ul>
      )}
      {advertencias.length > 0 && (
        <ul className="mt-3 space-y-1.5 text-sm text-texto-2">
          {advertencias.map((a) => (
            <li key={a} className="flex items-start gap-2">
              <Icono nombre="alerta" className="mt-0.5 size-4 shrink-0 text-aviso" />
              <span>{a}</span>
            </li>
          ))}
        </ul>
      )}

      <div className="mt-auto pt-4">
        <p className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm font-medium text-texto-2">
          <span className="inline-flex items-center gap-1.5">
            <Icono nombre="calendario" className="size-4 text-atenuado" />
            {duracionPlan(plan.semanas)} · {ETIQUETA_INTENSIDAD[plan.intensidad].toLowerCase()}
          </span>
          {plan.requiereCuestionario && (
            <span className="inline-flex items-center gap-1.5 text-atenuado">
              <Icono nombre="info" className="size-4" />
              Incluye unas preguntas de salud
            </span>
          )}
        </p>
        <Link
          to={`${base}/${encodeURIComponent(plan.slug)}`}
          className="mt-3 inline-flex min-h-11 items-center gap-1.5 rounded-lg font-semibold text-acento underline-offset-4 hover:text-acento-fuerte hover:underline"
        >
          Ver el plan
          {/* El nombre accesible contiene el texto visible (WCAG 2.5.3) más el plan. */}
          <span className="sr-only">: {plan.nombre}</span>
          <Icono nombre="flecha" className="size-4" />
        </Link>
      </div>
    </article>
  )
}
