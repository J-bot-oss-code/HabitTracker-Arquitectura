import { useState } from 'react'
import { Link, useLocation, useParams } from 'react-router'
import { useMisPlanes } from '@/features/mis-planes/api'
import {
  ETIQUETA_ALERGIA,
  ETIQUETA_CONDICION,
  ETIQUETA_LUGAR,
  ETIQUETA_ZONA,
  usePerfil,
} from '@/features/perfil/api'
import { useAuth } from '@/shared/auth/AuthContext'
import { esErrorHttp } from '@/shared/api/problem'
import { Alert } from '@/shared/ui/Alert'
import { Badge } from '@/shared/ui/Badge'
import { Button } from '@/shared/ui/Button'
import { Card, CardBody, EmptyState } from '@/shared/ui/Card'
import { claseBoton } from '@/shared/ui/claseBoton'
import { ErrorCarga } from '@/shared/ui/ErrorCarga'
import { Icono } from '@/shared/ui/Icono'
import { Esqueleto } from '@/shared/ui/Spinner'
import { usePlan, useRecomendados } from '../api'
import { EmpezarPlanDialogo } from '../components/EmpezarPlanDialogo'
import {
  DESCRIPCION_NIVEL,
  ETIQUETA_ANCLA,
  ETIQUETA_AREA,
  ETIQUETA_INTENSIDAD,
  ETIQUETA_NIVEL,
  ETIQUETA_PERIODICIDAD,
  ETIQUETA_SEXO_PLAN,
  duracionPlan,
} from '../etiquetas'
import {
  esSlugPlan,
  type PlanDetalleDto,
  type PlanNoAptoDto,
  type PlanRecomendadoDto,
} from '../types'

const lista = (textos: string[]) => textos.join(', ')

// Lo que la persona debe saber antes de abrir el plan: sale de los datos del plan, nunca de los suyos.
function notasDelPlan(plan: PlanDetalleDto): string[] {
  const notas: string[] = []
  if (plan.requiereCuestionario)
    notas.push(
      'Para empezarlo te haremos unas preguntas de salud, solo las que este plan necesita.',
    )
  if (plan.sexo !== 'Todos') notas.push(`${ETIQUETA_SEXO_PLAN[plan.sexo]}.`)
  if (plan.edadMinima > 16 || plan.edadMaxima < 100)
    notas.push(`Pensado para personas de ${plan.edadMinima} a ${plan.edadMaxima} años.`)
  if (plan.lugar) notas.push(`Se hace en: ${ETIQUETA_LUGAR[plan.lugar].toLowerCase()}.`)
  if (plan.zonasCorporales.length > 0)
    notas.push(
      `Trabaja: ${lista(plan.zonasCorporales.map((z) => ETIQUETA_ZONA[z].toLowerCase()))}.`,
    )
  if (plan.impacto) notas.push('Incluye ejercicios de impacto.')
  if (plan.contraindicaciones.length > 0)
    notas.push(
      `Consulta a tu médico antes de empezar si tienes: ${lista(plan.contraindicaciones.map((c) => ETIQUETA_CONDICION[c].toLowerCase()))}.`,
    )
  if (plan.alergenos.length > 0)
    notas.push(
      `Incluye alimentos con: ${lista(plan.alergenos.map((a) => ETIQUETA_ALERGIA[a].toLowerCase()))}.`,
    )
  return notas
}

function CargandoPlan() {
  return (
    <div role="status" aria-label="Cargando el plan" className="space-y-4 py-8">
      <Esqueleto className="h-5 w-40" />
      <Esqueleto className="h-10 w-3/4" />
      <Esqueleto className="h-24 w-full" />
      <Esqueleto className="h-48 w-full" />
    </div>
  )
}

function NoEncontrado({ base }: { base: string }) {
  return (
    <div className="py-10">
      <EmptyState
        nivelTitulo={1}
        icono="buscar"
        titulo="No encontramos ese plan"
        descripcion="Puede que se haya retirado del catálogo o que la dirección esté mal escrita."
        accion={
          <Link
            to={base}
            className="inline-flex min-h-11 items-center rounded-lg px-3 font-semibold text-acento underline"
          >
            Ver todos los planes
          </Link>
        }
      />
    </div>
  )
}

// El botón de acción depende de quién mira: sin sesión pide crear la cuenta; el Estándar ve si el plan es
// para él (el servidor decide) y puede empezarlo; el Administrador solo lo consulta.
function AccionDelPlan({ plan }: { plan: PlanDetalleDto }) {
  const { usuario } = useAuth()
  const esEstandar = usuario?.rol === 'Estandar'
  const recomendaciones = useRecomendados(esEstandar)
  const perfil = usePerfil(esEstandar)
  const misPlanes = useMisPlanes(esEstandar)
  const [abierto, setAbierto] = useState(false)

  if (!usuario) {
    return (
      <div className="flex flex-wrap items-center gap-3">
        <Link
          to={`/registro?meta=${encodeURIComponent(plan.slug)}`}
          className={claseBoton({ tamano: 'lg' })}
        >
          Empezar este plan
          <Icono nombre="flecha" className="size-4" />
        </Link>
        <p className="text-sm text-atenuado">Para empezar necesitas una cuenta gratis.</p>
      </div>
    )
  }
  if (!esEstandar) return null

  const apto: PlanRecomendadoDto | undefined = recomendaciones.data?.recomendados.find(
    (r) => r.plan.slug === plan.slug,
  )
  const noApto: PlanNoAptoDto | undefined = recomendaciones.data?.noAptos.find(
    (n) => n.plan.slug === plan.slug,
  )
  const siguiendo = misPlanes.data?.find(
    (p) => p.slug === plan.slug && (p.estado === 'Activo' || p.estado === 'Pausado'),
  )

  return (
    <div className="space-y-3">
      {noApto && (
        <Alert tono="info" titulo="No recomendado para ti">
          <p className="mb-1">Puedes leerlo, pero te sugerimos empezar por otro plan.</p>
          <ul className="space-y-1">
            {noApto.razones.map((r) => (
              <li key={r.motivo}>{r.mensaje}</li>
            ))}
          </ul>
        </Alert>
      )}
      {apto && (apto.motivos.length > 0 || apto.advertencias.length > 0) && (
        <ul className="space-y-1.5 text-sm text-texto-2">
          {apto.motivos.map((m) => (
            <li key={m} className="flex items-start gap-2">
              <Icono nombre="check" className="mt-0.5 size-4 shrink-0 text-acento" />
              <span>{m}</span>
            </li>
          ))}
          {apto.advertencias.map((a) => (
            <li key={a} className="flex items-start gap-2">
              <Icono nombre="alerta" className="mt-0.5 size-4 shrink-0 text-aviso" />
              <span>{a}</span>
            </li>
          ))}
        </ul>
      )}
      {siguiendo ? (
        <div className="flex flex-wrap items-center gap-3">
          <Badge tono="exito" icono="check">
            Ya lo estás siguiendo
          </Badge>
          <Link
            to={`/mis-planes/${siguiendo.id}`}
            className="inline-flex min-h-11 items-center font-semibold text-acento underline"
          >
            Ver mi avance
          </Link>
        </div>
      ) : (
        <Button
          tamano="lg"
          onClick={() => setAbierto(true)}
          disabled={
            !!noApto || !perfil.data || recomendaciones.isPending || recomendaciones.isError
          }
        >
          Empezar este plan
        </Button>
      )}
      <EmpezarPlanDialogo plan={plan} abierto={abierto} alCerrar={() => setAbierto(false)} />
      {!siguiendo && recomendaciones.isPending && (
        <p className="text-sm text-atenuado">Estamos comprobando si este plan encaja contigo…</p>
      )}
    </div>
  )
}

// Detalle de un plan (§5.1 y §5.2): tareas con su versión mínima, guía semanal y los avisos de seguridad.
// Existe en la parte pública (/metas/:slug) y dentro de la app (/planes/:slug); cambia solo el enlace de vuelta.
export function PlanDetallePage() {
  const { slug } = useParams()
  const { pathname } = useLocation()
  const base = pathname.startsWith('/planes') ? '/planes' : '/metas'
  const consulta = usePlan(slug)

  if (!esSlugPlan(slug)) return <NoEncontrado base={base} />
  if (consulta.isPending) return <CargandoPlan />
  if (consulta.isError) {
    return esErrorHttp(consulta.error, 404) ? (
      <NoEncontrado base={base} />
    ) : (
      <div className="py-8">
        <ErrorCarga
          error={consulta.error}
          porDefecto="No se pudo cargar el plan."
          alReintentar={() => consulta.refetch()}
        />
      </div>
    )
  }

  const plan = consulta.data
  const notas = notasDelPlan(plan)
  return (
    <article className="mx-auto max-w-3xl space-y-8 py-8">
      <div>
        <Link
          to={base}
          className="inline-flex min-h-11 items-center gap-1.5 rounded-lg font-semibold text-acento underline-offset-4 hover:underline"
        >
          <Icono nombre="volver" className="size-4" />
          Volver a los planes
        </Link>
        <p className="rotulo mt-2">{ETIQUETA_AREA[plan.area]}</p>
        <h1 className="mt-1 text-3xl font-extrabold sm:text-[2rem]">{plan.nombre}</h1>
        <div className="mt-3 flex flex-wrap gap-2">
          <Badge tono="marca">{ETIQUETA_NIVEL[plan.nivel]}</Badge>
          <Badge tono="neutro" icono="calendario">
            {duracionPlan(plan.semanas)}
          </Badge>
          <Badge tono="neutro">{ETIQUETA_INTENSIDAD[plan.intensidad]}</Badge>
        </div>
        <p className="mt-4 text-texto-2">{plan.descripcion}</p>
        <p className="mt-2 text-sm text-atenuado">{DESCRIPCION_NIVEL[plan.nivel]}</p>
      </div>

      <Alert tono="aviso" titulo="Antes de empezar">
        <p>{plan.avisoSeguridad}</p>
        {notas.length > 0 && (
          <ul className="mt-2 list-disc space-y-1 pl-5">
            {notas.map((n) => (
              <li key={n}>{n}</li>
            ))}
          </ul>
        )}
      </Alert>

      <AccionDelPlan plan={plan} />

      <section aria-labelledby="tareas-plan">
        <h2 id="tareas-plan" className="text-xl font-bold">
          Qué harás
        </h2>
        <ol className="mt-4 space-y-3">
          {plan.tareas.map((t) => (
            <li key={t.orden}>
              <Card>
                <CardBody>
                  <h3 className="font-bold">{t.nombre}</h3>
                  <p className="mt-1 text-sm text-texto-2">{t.instrucciones}</p>
                  <p className="mt-2 text-sm font-medium">
                    {t.cantidad} {t.unidad} {ETIQUETA_PERIODICIDAD[t.periodicidad]} ·{' '}
                    {ETIQUETA_ANCLA[t.anclaSugerida].toLowerCase()}
                  </p>
                  <p className="mt-2 text-sm text-atenuado">
                    <span className="font-semibold text-texto-2">Si hoy no puedes: </span>
                    {t.versionMinima}
                  </p>
                </CardBody>
              </Card>
            </li>
          ))}
        </ol>
      </section>

      {plan.guiaSemanal.length > 0 && (
        <section aria-labelledby="guia-plan">
          <h2 id="guia-plan" className="text-xl font-bold">
            Semana a semana
          </h2>
          <ol className="mt-4 divide-y divide-borde rounded-2xl border border-borde bg-superficie">
            {plan.guiaSemanal.map((s) => (
              <li key={s.semana} className="flex gap-4 p-4">
                <span className="w-20 shrink-0 text-sm font-bold text-acento-fuerte">
                  Semana {s.semana}
                </span>
                <span className="text-sm text-texto-2">{s.texto}</span>
              </li>
            ))}
          </ol>
        </section>
      )}

      <section aria-labelledby="beneficios-plan">
        <h2 id="beneficios-plan" className="text-xl font-bold">
          Qué ganas
        </h2>
        <ul className="mt-4 space-y-2">
          {plan.beneficios.map((b) => (
            <li key={b} className="flex items-start gap-3">
              <Icono nombre="check" className="mt-0.5 size-5 shrink-0 text-acento" />
              <span>{b}</span>
            </li>
          ))}
        </ul>
      </section>
    </article>
  )
}
