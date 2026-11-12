import { http, HttpResponse } from 'msw'
import {
  PERFILES_LABORALES,
  type Objetivo,
  type PerfilLaboral,
  type RestriccionSalud,
  type ZonaCorporal,
} from '@/features/perfil/types'
import {
  AREAS_PLAN,
  NIVELES_PLAN,
  esMiDia,
  type PlanDetalleDto,
  type PlanNoAptoDto,
  type PlanRecomendadoDto,
  type PlanResumenDto,
  type RazonNoAptoDto,
  type RecomendacionesDto,
} from '@/features/planes/types'
import { db, type PerfilMock } from '../db'
import { autenticar, latencia, problema, respuestas } from '../http'
import { evaluar } from './perfil'
import { PLANES_MOCK } from './planesData'

// Réplica de PlansController (HabitTracker v2, Fase 3): el catálogo y el detalle son anónimos y los
// recomendados son de cada persona. RecomendadorPlanes y ReglasSeguridadPlanes del backend están
// replicados abajo, con los mismos puntajes, textos y desempates.

const ROLES = ['Estandar' as const]

const resumen = (p: PlanDetalleDto): PlanResumenDto => ({
  slug: p.slug,
  nombre: p.nombre,
  resumen: p.resumen,
  area: p.area,
  nivel: p.nivel,
  intensidad: p.intensidad,
  semanas: p.semanas,
  sexo: p.sexo,
  edadMinima: p.edadMinima,
  edadMaxima: p.edadMaxima,
  perfilesLaborales: p.perfilesLaborales,
  beneficios: p.beneficios,
  requiereCuestionario: p.requiereCuestionario,
})

// StringComparer.Ordinal: comparación por unidades de código, no por idioma.
const ordinal = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0)
const rangoNivel = (n: PlanDetalleDto['nivel']) => NIVELES_PLAN.indexOf(n)

const planNoEncontrado = () =>
  problema(404, 'No encontrado.', 'No encontramos ese plan.', 'plan_no_encontrado')

// --- Variantes de "Mi día" (VariantesMiDia) ---

function miDiaDe(perfil: PerfilLaboral): string {
  switch (perfil) {
    case 'OficinaDiurna':
    case 'Remoto':
    case 'Estudiante':
    case 'SinEmpleo':
      return 'mi-dia-oficina'
    case 'TurnoNocturno':
    case 'TurnosRotativos':
      return 'mi-dia-turno-nocturno'
    case 'TrabajoFisico':
      return 'mi-dia-trabajo-fisico'
  }
}

const FRASE_JORNADA: Record<PerfilLaboral, string> = {
  OficinaDiurna: 'tu jornada de oficina',
  Remoto: 'tu trabajo remoto',
  Estudiante: 'tus días de estudio',
  SinEmpleo: 'tu día a día',
  TurnoNocturno: 'tu turno nocturno',
  TurnosRotativos: 'tus turnos rotativos',
  TrabajoFisico: 'tu trabajo físico o de pie',
}

// --- Objetivos del cuestionario → área del catálogo (RecomendadorPlanes.AreaDe) ---

const AREA_DE: Record<Objetivo, PlanDetalleDto['area']> = {
  PerderPeso: 'ControlDePeso',
  GanarMasaMuscular: 'Gimnasio',
  DefinirTonificar: 'Gimnasio',
  ReducirEstres: 'Estres',
  MejorarConcentracion: 'Concentracion',
  DormirMejor: 'Descanso',
  Hidratarme: 'Hidratacion',
  MasEnergia: 'Energia',
  PosturaYMovilidad: 'Movilidad',
}

const FRASE_OBJETIVO: Record<Objetivo, string> = {
  PerderPeso: 'cuidar tu peso',
  GanarMasaMuscular: 'ganar masa muscular',
  DefinirTonificar: 'definir y tonificar',
  ReducirEstres: 'reducir el estrés',
  MejorarConcentracion: 'concentrarte mejor',
  DormirMejor: 'dormir mejor',
  Hidratarme: 'hidratarte',
  MasEnergia: 'tener más energía',
  PosturaYMovilidad: 'postura y movilidad',
}

// --- ReglasSeguridadPlanes ---

const EDAD_MINIMA_PERMITIDA = 16
const EDAD_MAXIMA_PERMITIDA = 100
const AVISO_AGUA_MEDICO = 'Consulta a tu médico cuánta agua te conviene tomar al día.'
const AVISO_CONSULTAR_MEDICO = 'Antes de empezar, consulta a tu médico.'
const AVISO_COMPLETAR_EDAD = 'Agrega tu edad en tu perfil para confirmar que este plan es para ti.'
const AVISO_COMPLETAR_SEXO = 'Este plan está pensado para un sexo concreto; tu perfil no lo indica.'
const AVISO_PREGUNTAS_DE_SALUD = 'Antes de empezarlo te haremos unas preguntas de salud.'
const AVISO_NECESITA_GIMNASIO = 'Necesita un gimnasio.'

const CONDICIONES_CON_AVISO_DE_AGUA = ['Hipertension', 'EnfermedadCardiaca', 'EnfermedadRenal']

const ZONA_POR_RESTRICCION: Partial<Record<RestriccionSalud, ZonaCorporal>> = {
  EvitarZonaRodilla: 'GluteosYPiernas',
  EvitarZonaEspaldaBaja: 'Espalda',
  EvitarZonaHombro: 'BrazosYHombros',
}

interface Persona {
  perfil: PerfilMock
  evaluacion: ReturnType<typeof evaluar>
  objetivos: NonNullable<ReturnType<typeof cuestionarioUsado>>['objetivos']
  condiciones: string[]
  usaDatosDeSalud: boolean
}

// Sin el consentimiento datosSalud el cuestionario no se usa (aunque siga guardado).
const cuestionarioUsado = (id: string, usa: boolean) => (usa ? db.cuestionarios.get(id) : undefined)

function personaDe(usuarioId: string, perfil: PerfilMock): Persona {
  const usa = !!db.consentimientos.get(usuarioId)?.datosSalud
  const cuestionario = cuestionarioUsado(usuarioId, usa)
  return {
    perfil,
    evaluacion: evaluar(db.cuestionarios.get(usuarioId), perfil, usa),
    objetivos: cuestionario?.objetivos ?? null,
    condiciones: (cuestionario?.condiciones?.condiciones ?? []).filter((c) => c !== 'Ninguna'),
    usaDatosDeSalud: usa,
  }
}

const interseca = (a: readonly string[], b: readonly string[]) => a.some((x) => b.includes(x))

function evaluarPlan(plan: PlanDetalleDto, persona: Persona) {
  const razones: RazonNoAptoDto[] = []
  const advertencias: string[] = []
  const { perfil, evaluacion } = persona
  const restricciones = evaluacion.restricciones

  if (perfil.edad !== null) {
    if (perfil.edad < plan.edadMinima || perfil.edad > plan.edadMaxima)
      razones.push({
        motivo: 'FueraDeRangoDeEdad',
        mensaje: `Este plan es para personas de ${plan.edadMinima} a ${plan.edadMaxima} años.`,
      })
  } else if (plan.edadMinima > EDAD_MINIMA_PERMITIDA || plan.edadMaxima < EDAD_MAXIMA_PERMITIDA) {
    advertencias.push(AVISO_COMPLETAR_EDAD)
  }

  if (plan.sexo !== 'Todos') {
    if (perfil.sexo !== null) {
      if (plan.sexo !== perfil.sexo)
        razones.push({ motivo: 'SexoDelPlan', mensaje: 'Este plan está pensado para otro sexo.' })
    } else {
      advertencias.push(AVISO_COMPLETAR_SEXO)
    }
  }

  if (plan.area === 'ControlDePeso' && restricciones.includes('SinRestriccionCalorica'))
    razones.push({
      motivo: 'SinPlanesDePeso',
      mensaje: 'Por lo que nos contaste, ahora no recomendamos planes de control de peso.',
    })

  if (
    (restricciones.includes('SoloIntensidadBaja') && plan.intensidad !== 'Baja') ||
    (restricciones.includes('SinIntensidadAlta') && plan.intensidad === 'Alta')
  )
    razones.push({
      motivo: 'IntensidadNoRecomendada',
      mensaje: 'La intensidad de este plan no es la recomendada para ti ahora.',
    })

  if (plan.impacto && restricciones.includes('SinImpacto'))
    razones.push({
      motivo: 'ImpactoNoRecomendado',
      mensaje: 'Este plan tiene ejercicios de impacto que te conviene evitar.',
    })

  const zonasAEvitar = restricciones.flatMap((r) => ZONA_POR_RESTRICCION[r] ?? [])
  if (interseca(zonasAEvitar, plan.zonasCorporales))
    razones.push({
      motivo: 'ZonaAEvitar',
      mensaje: 'Este plan trabaja una zona que te conviene cuidar.',
    })

  if (interseca(plan.contraindicaciones, persona.condiciones))
    razones.push({
      motivo: 'Contraindicado',
      mensaje: 'Por lo que nos contaste, este plan no es recomendable para ti.',
    })

  if (interseca(plan.alergenos, evaluacion.alergenosAEvitar))
    razones.push({
      motivo: 'ContieneAlergenos',
      mensaje: 'Este plan incluye alimentos a los que indicaste alergia.',
    })

  const incluyeAgua = plan.tareas.some((t) => t.unidad === 'vasos' || t.unidad === 'litros')
  if (incluyeAgua && interseca(persona.condiciones, CONDICIONES_CON_AVISO_DE_AGUA))
    advertencias.push(AVISO_AGUA_MEDICO)
  if (evaluacion.nivel === 'ConsultarMedico') advertencias.push(AVISO_CONSULTAR_MEDICO)

  return { razones, advertencias }
}

// --- RecomendadorPlanes ---

const PUNTOS_MI_DIA = 1000
const PUNTOS_OBJETIVO = 30
const PUNTOS_JORNADA = 20
const PUNTOS_SIN_PREGUNTAS = 10
const PUNTOS_EDAD = 10
const PUNTOS_BASICO = 5
const PENALIZACION_PREGUNTAS = 10
const PENALIZACION_LUGAR = 15
const PENALIZACION_EXPERIENCIA = 10

function puntuar(
  plan: PlanDetalleDto,
  persona: Persona,
  personalizado: boolean,
  esSuMiDia: boolean,
) {
  let puntaje = 0
  const motivos: string[] = []
  const advertencias: string[] = []
  const { perfil } = persona

  if (esSuMiDia) {
    puntaje += PUNTOS_MI_DIA
    motivos.push(`Pensado para ${FRASE_JORNADA[perfil.perfilLaboral]}.`)
  } else if (plan.perfilesLaborales.includes(perfil.perfilLaboral)) {
    puntaje += PUNTOS_JORNADA
    motivos.push('Encaja con tu jornada.')
  }

  if (
    perfil.edad !== null &&
    (plan.edadMinima > EDAD_MINIMA_PERMITIDA || plan.edadMaxima < EDAD_MAXIMA_PERMITIDA)
  ) {
    puntaje += PUNTOS_EDAD
    motivos.push('Pensado para tu edad.')
  }

  if (personalizado && persona.objetivos) {
    for (const objetivo of persona.objetivos.objetivos.filter((o) => AREA_DE[o] === plan.area)) {
      puntaje += PUNTOS_OBJETIVO
      motivos.push(`Va con tu objetivo: ${FRASE_OBJETIVO[objetivo]}.`)
    }
    if (plan.lugar === 'Gimnasio' && persona.objetivos.lugar !== 'Gimnasio') {
      puntaje -= PENALIZACION_LUGAR
      advertencias.push(AVISO_NECESITA_GIMNASIO)
    }
    if (persona.objetivos.experiencia === 'Ninguna' && plan.intensidad !== 'Baja')
      puntaje -= PENALIZACION_EXPERIENCIA
  } else {
    // Sin cuestionario, primero lo que se empieza hoy en 3 toques (P1).
    if (!plan.requiereCuestionario) {
      puntaje += PUNTOS_SIN_PREGUNTAS
      if (!esSuMiDia) motivos.push('Lo empiezas hoy, sin preguntas de salud.')
    } else {
      puntaje -= PENALIZACION_PREGUNTAS
      advertencias.push(AVISO_PREGUNTAS_DE_SALUD)
    }
    if (plan.nivel === 'Basico') puntaje += PUNTOS_BASICO
  }

  if (
    personalizado &&
    plan.requiereCuestionario &&
    !persona.evaluacion.completitud.condiciones &&
    (plan.contraindicaciones.length > 0 || plan.alergenos.length > 0)
  )
    advertencias.push(AVISO_PREGUNTAS_DE_SALUD)

  return { puntaje, motivos, advertencias }
}

export function recomendar(usuarioId: string, perfil: PerfilMock): RecomendacionesDto {
  const persona = personaDe(usuarioId, perfil)
  const personalizado =
    persona.usaDatosDeSalud &&
    persona.evaluacion.completitud.objetivos &&
    persona.evaluacion.completitud.seguridad
  const suMiDia = miDiaDe(perfil.perfilLaboral)
  const recomendados: (PlanRecomendadoDto & { puntaje: number; plan: PlanResumenDto })[] = []
  const noAptos: PlanNoAptoDto[] = []

  for (const plan of PLANES_MOCK) {
    // Las variantes de "Mi día" de otras jornadas no se recomiendan: siguen en el catálogo.
    if (esMiDia(plan.slug) && plan.slug !== suMiDia) continue
    const evaluacion = evaluarPlan(plan, persona)
    if (evaluacion.razones.length > 0) {
      noAptos.push({ plan: resumen(plan), razones: evaluacion.razones })
      continue
    }
    const { puntaje, motivos, advertencias } = puntuar(
      plan,
      persona,
      personalizado,
      plan.slug === suMiDia,
    )
    recomendados.push({
      plan: resumen(plan),
      puntaje,
      motivos,
      advertencias: [...advertencias, ...evaluacion.advertencias],
    })
  }

  recomendados.sort(
    (a, b) =>
      b.puntaje - a.puntaje ||
      rangoNivel(a.plan.nivel) - rangoNivel(b.plan.nivel) ||
      ordinal(a.plan.slug, b.plan.slug),
  )
  noAptos.sort((a, b) => ordinal(a.plan.slug, b.plan.slug))
  return {
    recomendados: recomendados.map(({ plan, motivos, advertencias }) => ({
      plan,
      motivos,
      advertencias,
    })),
    noAptos,
    personalizadoConSalud: personalizado,
  }
}

export const planesHandlers = [
  http.get('/api/plans', async ({ request }) => {
    await latencia()
    const consulta = new URL(request.url).searchParams
    const area = consulta.get('area')
    const nivel = consulta.get('nivel')
    const jornada = consulta.get('perfilLaboral')
    // Un valor que no es del enum lo rechaza el enlace del modelo (400), igual que en el backend.
    if (
      (area !== null && !(AREAS_PLAN as readonly string[]).includes(area)) ||
      (nivel !== null && !(NIVELES_PLAN as readonly string[]).includes(nivel)) ||
      (jornada !== null && !(PERFILES_LABORALES as readonly string[]).includes(jornada))
    )
      return respuestas.invalida()

    const lista = PLANES_MOCK.filter((p) => area === null || p.area === area)
      .filter((p) => nivel === null || p.nivel === nivel)
      .filter(
        (p) =>
          jornada === null ||
          p.perfilesLaborales.length === 0 ||
          p.perfilesLaborales.includes(jornada as PerfilLaboral),
      )
      .sort((a, b) => rangoNivel(a.nivel) - rangoNivel(b.nivel) || ordinal(a.nombre, b.nombre))
      .map(resumen)
    return HttpResponse.json(lista)
  }),

  // Antes que /:slug, que también la captaría.
  http.get('/api/plans/recommended', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const perfil = db.perfiles.get(sesion.usuario.id)
    if (!perfil)
      return problema(
        404,
        'No encontrado.',
        'Aún no completaste tu perfil.',
        'perfil_no_encontrado',
      )
    return HttpResponse.json(recomendar(sesion.usuario.id, perfil))
  }),

  http.get('/api/plans/:slug', async ({ params }) => {
    await latencia()
    const plan = PLANES_MOCK.find((p) => p.slug === params.slug)
    return plan ? HttpResponse.json(plan) : planNoEncontrado()
  }),
]
