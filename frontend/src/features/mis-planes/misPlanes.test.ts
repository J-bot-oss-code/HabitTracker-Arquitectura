import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { beforeEach, describe, expect, it } from 'vitest'
import type { HabitoDto } from '@/features/habits/types'
import { PREGUNTAS_SEGURIDAD } from '@/features/perfil/types'
import {
  db,
  PLAN_DEMO_ID,
  PLAN_DEMO_SLUG,
  sembrarPlanDemo,
  type CuestionarioMock,
} from '@/mocks/db'
import { resolverAncla } from '@/mocks/handlers/misPlanes'
import { MENSAJES_MOTIVACION, TIPS_EXTRA } from '@/mocks/handlers/misPlanesData'
import { PLANES_MOCK } from '@/mocks/handlers/planesData'
import { CORREOS, iniciarSesionComo } from '@/test/render'
import type { HoyDto, PlanUsuarioDto, ResumenProgresoDto } from './types'

// La API simulada de Mis planes debe responder como MyPlansController y TodayController. Estas pruebas
// fijan las reglas del contrato que la interfaz da por supuestas; si el backend cambia, se actualizan
// aquí primero.
const url = (ruta: string) => `${window.location.origin}${ruta}`
const DIA = 86_400_000

type Quien = keyof typeof CORREOS

async function llamar(
  ruta: string,
  opciones: { metodo?: string; como?: Quien; token?: string; body?: unknown } = {},
) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  const token = opciones.token ?? (opciones.como ? iniciarSesionComo(opciones.como) : undefined)
  if (token) headers.Authorization = `Bearer ${token}`
  return fetch(url(ruta), {
    method: opciones.metodo ?? 'GET',
    headers,
    body: opciones.body === undefined ? undefined : JSON.stringify(opciones.body),
  })
}

const idDe = (quien: Quien) => db.usuarios.find((u) => u.correo === CORREOS[quien])!.id
const planEnDb = (id: string) => db.planesUsuario.find((p) => p.id === id)!
const habitosEnDb = (planId: string) => db.habitos.filter((h) => h.planUsuarioId === planId)

async function inscribir(slug: string, como: Quien = 'otro', extra: object = {}) {
  return llamar('/api/my-plans', { metodo: 'POST', como, body: { slug, ...extra } })
}

async function inscrito(slug: string, como: Quien = 'otro', extra: object = {}) {
  const respuesta = await inscribir(slug, como, extra)
  expect(respuesta.status).toBe(201)
  return (await respuesta.json()) as PlanUsuarioDto
}

const transicionar = (id: string, body: object, como: Quien = 'estandar') =>
  llamar(`/api/my-plans/${id}/transition`, { metodo: 'POST', como, body })
const ajustar = (id: string, tipo: string, como: Quien = 'estandar') =>
  llamar(`/api/my-plans/${id}/adjust`, { metodo: 'POST', como, body: { tipo } })
const hoyDe = async (como: Quien): Promise<HoyDto> => (await llamar('/api/today', { como })).json()
const planDe = async (id: string, como: Quien): Promise<PlanUsuarioDto> =>
  (await llamar(`/api/my-plans/${id}`, { como })).json()
const habitosDe = async (como: Quien): Promise<HabitoDto[]> =>
  (await llamar('/api/habits', { como })).json()

async function esperarCodigo(respuesta: Response, status: number, codigo: string | undefined) {
  expect(respuesta.status).toBe(status)
  const problema = await respuesta.json()
  expect(problema.codigo).toBe(codigo)
  expect(problema.status).toBe(status)
  return problema as { title: string; detail: string }
}

// Un cuestionario completo (objetivos, seguridad y condiciones), con el consentimiento de salud.
function responderCuestionario(quien: Quien, seguridadAfirmativa = false) {
  const id = idDe(quien)
  const ahora = new Date().toISOString()
  db.consentimientos.set(id, { ...db.consentimientos.get(id)!, datosSalud: true })
  db.cuestionarios.set(id, {
    objetivos: {
      objetivos: ['ReducirEstres'],
      zonas: [],
      experiencia: 'Principiante',
      lugar: 'CasaSinEquipo',
    },
    objetivosRespondidoEn: ahora,
    seguridad: Object.fromEntries(
      PREGUNTAS_SEGURIDAD.map((k) => [k, seguridadAfirmativa && k === 'dolorDePecho']),
    ) as NonNullable<CuestionarioMock['seguridad']>,
    seguridadRespondidoEn: ahora,
    condiciones: { condiciones: ['Ninguna'], alergias: ['Ninguna'] },
    condicionesRespondidoEn: ahora,
  })
}

// Ana sigue el plan de ejemplo (en las pruebas es opcional: ver sembrarPlanDemo en mocks/db.ts); Luis
// no sigue ninguno.
beforeEach(() => sembrarPlanDemo())

const BASICOS_SIN_PREGUNTAS = ['respiracion-anti-estres', 'caminar-para-empezar', 'dormir-mejor']
const CON_CUESTIONARIO = 'primeros-5-km'

describe('Mis planes: quién puede y qué ve', () => {
  it.each([
    ['GET', '/api/my-plans'],
    ['POST', '/api/my-plans'],
    ['GET', `/api/my-plans/${PLAN_DEMO_ID}`],
    ['POST', `/api/my-plans/${PLAN_DEMO_ID}/transition`],
    ['POST', `/api/my-plans/${PLAN_DEMO_ID}/adjust`],
    ['PUT', `/api/my-plans/${PLAN_DEMO_ID}/reminder`],
    ['GET', '/api/today'],
    ['GET', '/api/progress/summary'],
    ['POST', '/api/progress/achievements/seen'],
  ])('%s %s: sin token 401 y el Administrador 403', async (metodo, ruta) => {
    expect((await llamar(ruta, { metodo, body: metodo === 'GET' ? undefined : {} })).status).toBe(
      401,
    )
    expect(
      (await llamar(ruta, { metodo, como: 'admin', body: metodo === 'GET' ? undefined : {} }))
        .status,
    ).toBe(403)
  })

  it('el plan de ejemplo: enums como texto, fechas con "Z" y la hora ya formateada "HH:mm"', async () => {
    const respuesta = await llamar('/api/my-plans', { como: 'estandar' })
    expect(respuesta.status).toBe(200)
    const [plan, ...resto] = (await respuesta.json()) as PlanUsuarioDto[]
    expect(resto).toHaveLength(0)
    expect(plan).toMatchObject({
      id: PLAN_DEMO_ID,
      slug: PLAN_DEMO_SLUG,
      nombre: 'Mi día de oficina saludable',
      origen: 'Catalogo',
      estado: 'Activo',
      motivoPausa: null,
      semanas: 4,
      semanasTotales: 4,
      extensiones: 0,
      modoSuaveHasta: null,
      horaRecordatorio: '08:00',
      recordatoriosActivos: false,
      sugerencia: null,
      guia: [],
    })
    expect(plan.fechaInicio).toMatch(/Z$/)
    expect(plan.fechaFinPrevista).toMatch(/Z$/)
    expect(Date.parse(plan.fechaFinPrevista) - Date.parse(plan.fechaInicio)).toBe(28 * DIA)
  })

  it('el progreso del plan de ejemplo es coherente: semana 2 de 4, 19 días restantes y racha de 9', async () => {
    const { progreso } = await planDe(PLAN_DEMO_ID, 'estandar')
    expect(progreso).toMatchObject({
      unidad: 'Dias',
      semanaActual: 2,
      semanasTotales: 4,
      diasRestantes: 19,
      // Un día sin registro no corta la racha.
      racha: 9,
      rachaMasLarga: 9,
      diasCumplidos: 9,
      diasSinRegistroSeguidos: 0,
      cumplidoHoy: true,
      semanasCelebradas: [1],
    })
    // Últimos 7 días: 3 completos, 1 con la versión mínima, 2 parciales y 1 sin registro.
    expect(progreso.siete).toEqual({
      diasCompletos: 3,
      diasMinimos: 1,
      diasElegibles: 7,
      porcentajeCompleto: 42.9,
      porcentajeConMinimo: 57.1,
    })
    expect(progreso.treinta.diasElegibles).toBe(10)
  })

  it('los hábitos del plan llevan su versión mínima, su ancla y la hora que le toca en la jornada', async () => {
    const { habitos } = await planDe(PLAN_DEMO_ID, 'estandar')
    expect(habitos.map((h) => [h.ancla, h.horaSugerida, h.estado])).toEqual([
      ['AlLlegarAlTrabajo', '08:00', 'Activo'],
      ['AMediaManana', '10:15', 'Activo'],
      ['AMediaTarde', '14:45', 'Activo'],
    ])
    expect(habitos[0]).toMatchObject({
      nombre: 'Tomar agua durante la jornada',
      periodicidad: 'Diaria',
      cantidadObjetivo: 8,
      unidad: 'vasos',
      versionMinima: 'Toma 2 vasos de agua, uno al llegar y otro al salir.',
    })
    expect(habitos[0].descripcion).toEqual(expect.any(String))
  })

  it('quien no sigue ningún plan recibe una lista vacía', async () => {
    expect(await (await llamar('/api/my-plans', { como: 'otro' })).json()).toEqual([])
  })

  it('un plan de otra persona es 404 plan_usuario_no_encontrado, igual que uno que no existe', async () => {
    const ajeno = await llamar(`/api/my-plans/${PLAN_DEMO_ID}`, { como: 'otro' })
    const problema = await esperarCodigo(ajeno, 404, 'plan_usuario_no_encontrado')
    expect(problema.title).toBe('No encontrado.')

    const inexistente = await llamar('/api/my-plans/00000000-0000-4000-8000-000000000999', {
      como: 'estandar',
    })
    await esperarCodigo(inexistente, 404, 'plan_usuario_no_encontrado')

    // Tampoco se puede cambiar, ajustar ni configurar.
    await esperarCodigo(
      await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Cancelado' }, 'otro'),
      404,
      'plan_usuario_no_encontrado',
    )
    await esperarCodigo(
      await ajustar(PLAN_DEMO_ID, 'Extender', 'otro'),
      404,
      'plan_usuario_no_encontrado',
    )
    const recordatorio = await llamar(`/api/my-plans/${PLAN_DEMO_ID}/reminder`, {
      metodo: 'PUT',
      como: 'otro',
      body: { hora: '07:00:00', recordatoriosActivos: true },
    })
    await esperarCodigo(recordatorio, 404, 'plan_usuario_no_encontrado')
    expect(planEnDb(PLAN_DEMO_ID)).toMatchObject({ estado: 'Activo', extensiones: 0 })
  })

  it('un id que no es GUID es 404 sin cuerpo (ruta {id:guid})', async () => {
    const respuesta = await llamar('/api/my-plans/no-es-guid', { como: 'estandar' })
    expect(respuesta.status).toBe(404)
    expect(await respuesta.text()).toBe('')
  })
})

describe('POST /api/my-plans (inscribirse)', () => {
  it('201 con Location: el plan nace Activo y crea sus hábitos ya activos', async () => {
    const respuesta = await inscribir('respiracion-anti-estres')
    expect(respuesta.status).toBe(201)
    const plan = (await respuesta.json()) as PlanUsuarioDto
    expect(respuesta.headers.get('Location')).toBe(`/api/my-plans/${plan.id}`)
    expect(plan).toMatchObject({
      slug: 'respiracion-anti-estres',
      origen: 'Catalogo',
      estado: 'Activo',
      semanas: 2,
      semanasTotales: 2,
      extensiones: 0,
      recordatoriosActivos: false,
    })
    expect(plan.progreso).toMatchObject({
      semanaActual: 1,
      diasRestantes: 14,
      racha: 0,
      cumplidoHoy: false,
      unidad: 'Dias',
    })
    expect(Date.parse(plan.fechaFinPrevista) - Date.parse(plan.fechaInicio)).toBe(14 * DIA)

    // Los hábitos del plan aparecen en GET /api/habits con sus campos nuevos.
    const delPlan = (await habitosDe('otro')).filter((h) => h.planUsuarioId === plan.id)
    const plantilla = PLANES_MOCK.find((p) => p.slug === 'respiracion-anti-estres')!
    expect(delPlan).toHaveLength(plantilla.tareas.length)
    expect(
      delPlan.map((h) => [h.nombre, h.estado, h.descripcion, h.versionMinima, h.ancla]),
    ).toEqual(
      plantilla.tareas.map((t) => [
        t.nombre,
        'Activo',
        t.instrucciones,
        t.versionMinima,
        t.anclaSugerida,
      ]),
    )
    expect(plan.habitos.map((h) => h.habitoId)).toEqual(delPlan.map((h) => h.id))
  })

  it('la hora del recordatorio: la que llega manda; si no, el ancla elegida o la de la primera tarea', async () => {
    // Luis trabaja de noche (22:00 a 06:00): "a media tarde" de su jornada son las 04:00.
    const porDefecto = await inscrito('respiracion-anti-estres')
    expect(porDefecto.horaRecordatorio).toBe('04:00')
    const conAncla = await inscrito('caminar-para-empezar', 'otro', { ancla: 'AntesDeDormir' })
    expect(conAncla.horaRecordatorio).toBe('07:00')
    const conHora = await inscrito('dormir-mejor', 'otro', {
      ancla: 'AntesDeDormir',
      hora: '21:30:00',
    })
    expect(conHora.horaRecordatorio).toBe('21:30')
  })

  it.each([
    ['sin slug', {}],
    ['slug vacío', { slug: '  ' }],
    ['slug de más de 60 caracteres', { slug: 'a'.repeat(61) }],
    ['ancla desconocida', { slug: 'dormir-mejor', ancla: 'AlAtardecer' }],
    ['hora mal escrita', { slug: 'dormir-mejor', hora: '25:00' }],
  ])('400 con %s', async (_caso, body) => {
    const respuesta = await llamar('/api/my-plans', { metodo: 'POST', como: 'otro', body })
    await esperarCodigo(respuesta, 400, undefined)
  })

  it('404 plan_no_encontrado con un slug que no está en el catálogo', async () => {
    await esperarCodigo(await inscribir('plan-que-no-existe'), 404, 'plan_no_encontrado')
  })

  it('404 perfil_no_encontrado antes del onboarding', async () => {
    db.perfiles.delete(idDe('otro'))
    await esperarCodigo(await inscribir('dormir-mejor'), 404, 'perfil_no_encontrado')
  })

  it('409 consentimiento_salud_requerido solo si el plan pide el cuestionario', async () => {
    const id = idDe('otro')
    db.consentimientos.set(id, { ...db.consentimientos.get(id)!, datosSalud: false })

    const problema = await esperarCodigo(
      await inscribir(CON_CUESTIONARIO),
      409,
      'consentimiento_salud_requerido',
    )
    expect(problema.title).toBe('Falta el consentimiento.')
    // Un Básico sin requisitos de salud se empieza sin el consentimiento ni el cuestionario.
    expect((await inscribir('respiracion-anti-estres')).status).toBe(201)
  })

  it('400 cuestionario_incompleto si faltan los bloques que el plan necesita', async () => {
    // Luis dio el consentimiento, pero no respondió el cuestionario.
    await esperarCodigo(await inscribir(CON_CUESTIONARIO), 400, 'cuestionario_incompleto')
    responderCuestionario('otro')
    expect((await inscribir(CON_CUESTIONARIO)).status).toBe(201)
  })

  it('400 plan_no_apto con la razón que da la regla de seguridad', async () => {
    // Una respuesta afirmativa de seguridad solo deja planes de intensidad baja.
    responderCuestionario('otro', true)
    const plan = PLANES_MOCK.find((p) => p.intensidad !== 'Baja')!
    const problema = await esperarCodigo(await inscribir(plan.slug), 400, 'plan_no_apto')
    expect(problema.detail).toBe('La intensidad de este plan no es la recomendada para ti ahora.')
    expect(db.planesUsuario.filter((p) => p.usuarioId === idDe('otro'))).toHaveLength(0)
  })

  it('400 plan_ya_activo: el mismo plan no se repite mientras esté en curso', async () => {
    await esperarCodigo(await inscribir(PLAN_DEMO_SLUG, 'estandar'), 400, 'plan_ya_activo')
    // En pausa sigue en curso.
    await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Pausado', motivoPausa: 'Viaje' })
    await esperarCodigo(await inscribir(PLAN_DEMO_SLUG, 'estandar'), 400, 'plan_ya_activo')
    // Cancelado ya no: se puede empezar de nuevo.
    await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Cancelado' })
    expect((await inscribir(PLAN_DEMO_SLUG, 'estandar')).status).toBe(201)
  })

  it('400 limite_planes con 3 planes en curso; al cancelar uno se puede empezar otro', async () => {
    // Ana ya sigue uno.
    const [segundo, tercero, cuarto] = BASICOS_SIN_PREGUNTAS
    await inscrito(segundo, 'estandar')
    const plan = await inscrito(tercero, 'estandar')
    const problema = await esperarCodigo(await inscribir(cuarto, 'estandar'), 400, 'limite_planes')
    expect(problema.detail).toMatch(/3 planes a la vez/)

    await transicionar(plan.id, { nuevoEstado: 'Cancelado' })
    expect((await inscribir(cuarto, 'estandar')).status).toBe(201)
    const resumen: ResumenProgresoDto = await (
      await llamar('/api/progress/summary', { como: 'estandar' })
    ).json()
    expect(resumen.planesEnCurso).toBe(3)
    // La lista trae todos, también el cancelado, del más reciente al más antiguo.
    const lista: PlanUsuarioDto[] = await (
      await llamar('/api/my-plans', { como: 'estandar' })
    ).json()
    expect(lista).toHaveLength(4)
    expect(lista[lista.length - 1].id).toBe(PLAN_DEMO_ID)
  })
})

describe('POST /api/my-plans/{id}/transition', () => {
  it('pausar exige un motivo (400 sin código) y pausa los hábitos del plan', async () => {
    const sinMotivo = await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Pausado' })
    const problema = await esperarCodigo(sinMotivo, 400, undefined)
    expect(problema.title).toBe('Solicitud inválida.')

    const respuesta = await transicionar(PLAN_DEMO_ID, {
      nuevoEstado: 'Pausado',
      motivoPausa: 'Viaje',
    })
    expect(respuesta.status).toBe(200)
    const plan = (await respuesta.json()) as PlanUsuarioDto
    expect(plan).toMatchObject({ estado: 'Pausado', motivoPausa: 'Viaje', sugerencia: null })
    expect(plan.habitos.map((h) => h.estado)).toEqual(['Pausado', 'Pausado', 'Pausado'])

    // Un plan en pausa no aporta tareas a Hoy, pero sigue en curso.
    const hoy = await hoyDe('estandar')
    expect(hoy.tareas).toEqual([])
    expect(hoy.planes).toEqual([])
    expect(hoy.mensaje).toBeNull()
  })

  it('reanudar devuelve solo los hábitos que el plan pausó y alarga el plan los días de la pausa', async () => {
    const [agua, pausas, vista] = habitosEnDb(PLAN_DEMO_ID)
    // La persona pausó a mano uno de los hábitos antes de pausar el plan.
    const aMano = await llamar(`/api/habits/${agua.id}/transition`, {
      metodo: 'POST',
      como: 'estandar',
      body: { nuevoEstado: 'Pausado' },
    })
    expect(aMano.status).toBe(204)
    await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Pausado', motivoPausa: 'Enfermedad' })
    // La pausa empezó hace 3 días.
    const plan = planEnDb(PLAN_DEMO_ID)
    const finAntes = Date.parse(plan.fechaFinPrevista)
    plan.pausas[0].inicioUtc = new Date(Date.now() - 3 * DIA).toISOString()

    const respuesta = await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Activo' })
    expect(respuesta.status).toBe(200)
    const reanudado = (await respuesta.json()) as PlanUsuarioDto
    expect(reanudado).toMatchObject({ estado: 'Activo', motivoPausa: null })
    expect(Date.parse(reanudado.fechaFinPrevista) - finAntes).toBe(3 * DIA)
    expect([agua.estado, pausas.estado, vista.estado]).toEqual(['Pausado', 'Activo', 'Activo'])
  })

  it('transiciones prohibidas: 400 transicion_plan_invalida', async () => {
    // Activo → Activo.
    await esperarCodigo(
      await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Activo' }),
      400,
      'transicion_plan_invalida',
    )
    // Pausado → Completado y Pausado → Pausado.
    await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Pausado', motivoPausa: 'Trabajo' })
    const problema = await esperarCodigo(
      await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Completado' }),
      400,
      'transicion_plan_invalida',
    )
    expect(problema.title).toBe('La operación no pudo completarse.')
    await esperarCodigo(
      await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Pausado', motivoPausa: 'Otro' }),
      400,
      'transicion_plan_invalida',
    )
    expect(planEnDb(PLAN_DEMO_ID).estado).toBe('Pausado')
  })

  it('completar antes de la fecha de fin es 400 plan_aun_no_termina', async () => {
    await esperarCodigo(
      await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Completado' }),
      400,
      'plan_aun_no_termina',
    )
  })

  it('al llegar a fechaFinPrevista se completa (no se pausa) y los hábitos quedan en pausa', async () => {
    planEnDb(PLAN_DEMO_ID).fechaFinPrevista = new Date(Date.now() - 60_000).toISOString()
    await esperarCodigo(
      await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Pausado', motivoPausa: 'Viaje' }),
      400,
      'transicion_plan_invalida',
    )

    const respuesta = await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Completado' })
    expect(respuesta.status).toBe(200)
    const plan = (await respuesta.json()) as PlanUsuarioDto
    expect(plan.estado).toBe('Completado')
    expect(plan.habitos.map((h) => h.estado)).toEqual(['Pausado', 'Pausado', 'Pausado'])

    // Completado es terminal: no cambia ni se ajusta.
    for (const nuevoEstado of ['Activo', 'Pausado', 'Cancelado', 'Completado']) {
      await esperarCodigo(
        await transicionar(PLAN_DEMO_ID, { nuevoEstado, motivoPausa: 'Otro' }),
        400,
        'transicion_plan_invalida',
      )
    }
    await esperarCodigo(await ajustar(PLAN_DEMO_ID, 'Extender'), 400, 'ajuste_no_disponible')
  })

  it('con conservarHabitos los hábitos siguen activos y vinculados al plan', async () => {
    planEnDb(PLAN_DEMO_ID).fechaFinPrevista = new Date(Date.now() - 60_000).toISOString()
    const respuesta = await transicionar(PLAN_DEMO_ID, {
      nuevoEstado: 'Completado',
      conservarHabitos: true,
    })
    const plan = (await respuesta.json()) as PlanUsuarioDto
    expect(plan.habitos.map((h) => h.estado)).toEqual(['Activo', 'Activo', 'Activo'])
    const delPlan = (await habitosDe('estandar')).filter((h) => h.planUsuarioId === PLAN_DEMO_ID)
    expect(delPlan).toHaveLength(3)
    // Un plan terminado ya no aporta tareas a Hoy.
    expect((await hoyDe('estandar')).tareas).toEqual([])
  })

  it('cancelar vale desde Activo y desde Pausado, y es terminal', async () => {
    await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Pausado', motivoPausa: 'Lesion' })
    const respuesta = await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Cancelado' })
    expect(respuesta.status).toBe(200)
    expect(await respuesta.json()).toMatchObject({ estado: 'Cancelado', motivoPausa: null })
    await esperarCodigo(
      await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Activo' }),
      400,
      'transicion_plan_invalida',
    )

    const otro = await inscrito('dormir-mejor')
    const cancelado = await transicionar(otro.id, { nuevoEstado: 'Cancelado' }, 'otro')
    const plan = (await cancelado.json()) as PlanUsuarioDto
    expect(plan.estado).toBe('Cancelado')
    expect(plan.habitos.every((h) => h.estado === 'Pausado')).toBe(true)
  })

  it.each([
    ['sin nuevoEstado', {}],
    ['estado desconocido', { nuevoEstado: 'Archivado' }],
    ['motivo desconocido', { nuevoEstado: 'Pausado', motivoPausa: 'Pereza' }],
    ['conservarHabitos que no es booleano', { nuevoEstado: 'Cancelado', conservarHabitos: 'si' }],
  ])('400 con %s', async (_caso, body) => {
    await esperarCodigo(await transicionar(PLAN_DEMO_ID, body), 400, undefined)
    expect(planEnDb(PLAN_DEMO_ID).estado).toBe('Activo')
  })
})

describe('POST /api/my-plans/{id}/adjust', () => {
  it('el modo suave dura 7 días y no se activa dos veces', async () => {
    const antes = Date.now()
    const respuesta = await ajustar(PLAN_DEMO_ID, 'ModoSuave')
    expect(respuesta.status).toBe(200)
    const plan = (await respuesta.json()) as PlanUsuarioDto
    const hasta = Date.parse(plan.modoSuaveHasta!)
    expect(hasta).toBeGreaterThanOrEqual(antes + 7 * DIA)
    expect(hasta).toBeLessThanOrEqual(Date.now() + 7 * DIA)

    const problema = await esperarCodigo(
      await ajustar(PLAN_DEMO_ID, 'ModoSuave'),
      400,
      'ajuste_no_disponible',
    )
    expect(problema.detail).toBe('El modo suave ya está activo.')
  })

  it('en modo suave la versión mínima cuenta como día completo', async () => {
    // Hoy, con las tres tareas: dos completas y una con la mínima. Sin modo suave el día es "mínimo".
    const [, , vista] = habitosEnDb(PLAN_DEMO_ID)
    await llamar(`/api/habits/${vista.id}/progress`, {
      metodo: 'POST',
      como: 'estandar',
      body: { cantidad: 3, fechaUtc: new Date().toISOString() },
    })
    const sinSuave = (await planDe(PLAN_DEMO_ID, 'estandar')).progreso.siete
    await ajustar(PLAN_DEMO_ID, 'ModoSuave')
    const conSuave = (await planDe(PLAN_DEMO_ID, 'estandar')).progreso.siete
    expect(conSuave.diasCompletos).toBe(sinSuave.diasCompletos + 1)
    expect(conSuave.diasMinimos).toBe(sinSuave.diasMinimos - 1)
  })

  it('extender suma una semana y se puede 2 veces como máximo (400 limite_extensiones)', async () => {
    const fin = Date.parse(planEnDb(PLAN_DEMO_ID).fechaFinPrevista)
    const primera = (await (await ajustar(PLAN_DEMO_ID, 'Extender')).json()) as PlanUsuarioDto
    expect(primera).toMatchObject({ extensiones: 1, semanas: 4, semanasTotales: 5 })
    expect(Date.parse(primera.fechaFinPrevista)).toBe(fin + 7 * DIA)
    expect(primera.progreso).toMatchObject({ semanasTotales: 5, diasRestantes: 26 })

    const segunda = (await (await ajustar(PLAN_DEMO_ID, 'Extender')).json()) as PlanUsuarioDto
    expect(segunda).toMatchObject({ extensiones: 2, semanasTotales: 6 })
    expect(Date.parse(segunda.fechaFinPrevista)).toBe(fin + 14 * DIA)

    const problema = await esperarCodigo(
      await ajustar(PLAN_DEMO_ID, 'Extender'),
      400,
      'limite_extensiones',
    )
    expect(problema.detail).toMatch(/2 veces como máximo/)
    expect(planEnDb(PLAN_DEMO_ID).extensiones).toBe(2)
  })

  it('"Pausar" no es un ajuste (es una transición con motivo) y un tipo desconocido tampoco: 400', async () => {
    await esperarCodigo(await ajustar(PLAN_DEMO_ID, 'Pausar'), 400, undefined)
    await esperarCodigo(await ajustar(PLAN_DEMO_ID, 'Acortar'), 400, undefined)
    expect(planEnDb(PLAN_DEMO_ID).estado).toBe('Activo')
  })

  it('solo se ajusta un plan activo: en pausa es 400 ajuste_no_disponible', async () => {
    await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Pausado', motivoPausa: 'Viaje' })
    for (const tipo of ['ModoSuave', 'Extender'])
      await esperarCodigo(await ajustar(PLAN_DEMO_ID, tipo), 400, 'ajuste_no_disponible')
  })
})

describe('PUT /api/my-plans/{id}/reminder', () => {
  const recordatorio = (body: object) =>
    llamar(`/api/my-plans/${PLAN_DEMO_ID}/reminder`, { metodo: 'PUT', como: 'estandar', body })

  it('guarda la hora (entra "HH:mm:ss", sale "HH:mm") y la activación', async () => {
    const respuesta = await recordatorio({ hora: '07:15:00', recordatoriosActivos: true })
    expect(respuesta.status).toBe(200)
    expect(await respuesta.json()).toMatchObject({
      horaRecordatorio: '07:15',
      recordatoriosActivos: true,
    })
  })

  it('sin hora conserva la que el plan ya tenía', async () => {
    const apagado = await recordatorio({ hora: null, recordatoriosActivos: false })
    expect(await apagado.json()).toMatchObject({
      horaRecordatorio: '08:00',
      recordatoriosActivos: false,
    })
    const encendido = await recordatorio({ hora: null, recordatoriosActivos: true })
    expect(await encendido.json()).toMatchObject({
      horaRecordatorio: '08:00',
      recordatoriosActivos: true,
    })
  })

  it('400 con una hora mal escrita, o al activarlo sin que haya ninguna hora', async () => {
    await esperarCodigo(
      await recordatorio({ hora: '7 de la mañana', recordatoriosActivos: true }),
      400,
      undefined,
    )
    planEnDb(PLAN_DEMO_ID).horaRecordatorio = null
    await esperarCodigo(
      await recordatorio({ hora: null, recordatoriosActivos: true }),
      400,
      undefined,
    )
    expect(planEnDb(PLAN_DEMO_ID).recordatoriosActivos).toBe(false)
  })
})

describe('GET /api/today', () => {
  it('las tareas de los planes activos con su estado: Hecho, Minimo y Pendiente', async () => {
    const hoy = await hoyDe('estandar')
    expect(hoy.fecha).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    expect(
      hoy.tareas.map((t) => [t.nombre, t.estado, t.cantidadPeriodo, t.restante, t.horaSugerida]),
    ).toEqual([
      ['Tomar agua durante la jornada', 'Hecho', 8, 0, '08:00'],
      ['Pausa activa cada 2 horas', 'Minimo', 1, 2, '10:15'],
      ['Descanso de la vista y postura', 'Pendiente', 0, 3, '14:45'],
    ])
    expect(hoy.tareas[0]).toMatchObject({
      planId: PLAN_DEMO_ID,
      planNombre: 'Mi día de oficina saludable',
      periodicidad: 'Diaria',
      cantidadObjetivo: 8,
      unidad: 'vasos',
      ancla: 'AlLlegarAlTrabajo',
    })
    expect(hoy.tareas[0].instrucciones).toEqual(expect.any(String))
    expect(hoy.tareas[0].versionMinima).toEqual(expect.any(String))
  })

  it('el plan en Hoy coincide con Mis planes: semana, días restantes y racha', async () => {
    const hoy = await hoyDe('estandar')
    const { progreso } = await planDe(PLAN_DEMO_ID, 'estandar')
    expect(hoy.planes).toEqual([
      {
        planId: PLAN_DEMO_ID,
        nombre: 'Mi día de oficina saludable',
        semanaActual: progreso.semanaActual,
        semanasTotales: progreso.semanasTotales,
        diasRestantes: progreso.diasRestantes,
        racha: progreso.racha,
        sugerencia: null,
      },
    ])
  })

  it('el mensaje del día es de constancia, y el mismo todo el día', async () => {
    const constancia = MENSAJES_MOTIVACION.filter((m) => m.categoria === 'Constancia').map(
      (m) => m.texto,
    )
    const hoy = await hoyDe('estandar')
    expect(constancia).toContain(hoy.mensaje)
    expect((await hoyDe('estandar')).mensaje).toBe(hoy.mensaje)
  })

  it('celebra la semana recién cerrada con 5 de 7 días; sin guía no hay tip extra', async () => {
    const hoy = await hoyDe('estandar')
    expect(hoy.celebracionSemana).toBe(1)
    expect(hoy.tipExtra).toBeNull()
    // Los logros del ejemplo ya se celebraron.
    expect(hoy.logrosNuevos).toEqual([])
  })

  it('sin planes: ni tareas, ni mensaje, ni celebración', async () => {
    expect(await hoyDe('otro')).toMatchObject({
      tareas: [],
      planes: [],
      mensaje: null,
      celebracionSemana: null,
      tipExtra: null,
      logrosNuevos: [],
    })
  })

  it('un avance con esVersionMinima deja la tarea en Minimo; al llegar a la meta pasa a Hecho', async () => {
    const [, , vista] = habitosEnDb(PLAN_DEMO_ID)
    const avance = (body: object) =>
      llamar(`/api/habits/${vista.id}/progress`, {
        metodo: 'POST',
        como: 'estandar',
        body: { fechaUtc: new Date().toISOString(), ...body },
      })
    const tarea = async () => (await hoyDe('estandar')).tareas.find((t) => t.habitoId === vista.id)!

    expect((await avance({ cantidad: 1, esVersionMinima: true })).status).toBe(204)
    expect(await tarea()).toMatchObject({ estado: 'Minimo', cantidadPeriodo: 1, restante: 2 })
    // El registro guarda la marca.
    const habito = (await habitosDe('estandar')).find((h) => h.id === vista.id)!
    expect(habito.registros[habito.registros.length - 1].esVersionMinima).toBe(true)

    // Un avance normal que no llega a la meta: ya no es "solo lo mínimo".
    await avance({ cantidad: 1 })
    expect(await tarea()).toMatchObject({ estado: 'Pendiente', cantidadPeriodo: 2, restante: 1 })
    await avance({ cantidad: 1 })
    expect(await tarea()).toMatchObject({ estado: 'Hecho', cantidadPeriodo: 3, restante: 0 })
  })

  it('sugiere ajustar con 4 días sin registro de los últimos 7, no con 3', async () => {
    const plan = await inscrito('respiracion-anti-estres')
    const empezoHace = (dias: number) =>
      (planEnDb(plan.id).fechaInicio = new Date(Date.now() - dias * DIA).toISOString())

    empezoHace(3)
    expect((await hoyDe('otro')).planes[0].sugerencia).toBeNull()
    expect((await planDe(plan.id, 'otro')).sugerencia).toBeNull()

    empezoHace(4)
    const sugerencia = { opciones: ['ModoSuave', 'Extender', 'Pausar'], diasSinRegistro: 4 }
    expect((await hoyDe('otro')).planes[0].sugerencia).toEqual(sugerencia)
    expect((await planDe(plan.id, 'otro')).sugerencia).toEqual(sugerencia)
    // Con 2 días seguidos o más sin registro el mensaje invita a volver, sin reproches.
    const regreso = MENSAJES_MOTIVACION.filter((m) => m.categoria === 'Regreso').map((m) => m.texto)
    expect(regreso).toContain((await hoyDe('otro')).mensaje)

    // Lo que ya se aplicó deja de ofrecerse.
    await ajustar(plan.id, 'ModoSuave', 'otro')
    expect((await planDe(plan.id, 'otro')).sugerencia?.opciones).toEqual(['Extender', 'Pausar'])
    await ajustar(plan.id, 'Extender', 'otro')
    await ajustar(plan.id, 'Extender', 'otro')
    expect((await planDe(plan.id, 'otro')).sugerencia?.opciones).toEqual(['Pausar'])
  })

  it('una pausa con motivo no cuenta como días sin registro ni corta la racha', async () => {
    await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Pausado', motivoPausa: 'Viaje' })
    const plan = planEnDb(PLAN_DEMO_ID)
    // La pausa empezó hace 5 días: desde entonces solo hubo avance algunos días.
    plan.pausas[0].inicioUtc = new Date(Date.now() - 5 * DIA).toISOString()
    for (const habito of habitosEnDb(PLAN_DEMO_ID))
      habito.registros = habito.registros.filter(
        (r) => Date.parse(r.fecha) < Date.now() - 5.5 * DIA,
      )

    const pausado = await planDe(PLAN_DEMO_ID, 'estandar')
    // Quedan los días 0 a 3 del plan: tres cumplidos y uno sin registro.
    expect(pausado.progreso).toMatchObject({ racha: 3, diasCumplidos: 3, cumplidoHoy: false })
    await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Activo' })
    const reanudado = await planDe(PLAN_DEMO_ID, 'estandar')
    expect(reanudado.progreso).toMatchObject({ racha: 3, diasSinRegistroSeguidos: 1 })
    expect(reanudado.sugerencia).toBeNull()
  })

  it('con guía semanal, celebrar la semana desbloquea su tip extra', async () => {
    const plan = await inscrito('caminar-para-empezar')
    planEnDb(plan.id).fechaInicio = new Date(Date.now() - 7 * DIA).toISOString()
    const [caminar] = habitosEnDb(plan.id)
    // Caminó 5 de los 7 días de la semana 1.
    caminar.registros = [7, 6, 5, 4, 3].map((dias, i) => ({
      id: `aa000000-0000-4000-8000-00000000000${i}`,
      cantidad: 10,
      fecha: new Date(Date.now() - dias * DIA).toISOString(),
      unidad: caminar.unidad,
    }))

    const tips = TIPS_EXTRA['caminar-para-empezar']
    const hoy = await hoyDe('otro')
    expect(hoy.celebracionSemana).toBe(1)
    expect(hoy.tipExtra).toBe(tips[0])
    const detalle = await planDe(plan.id, 'otro')
    expect(detalle.progreso).toMatchObject({ semanaActual: 2, semanasCelebradas: [1] })
    expect(detalle.guia).toHaveLength(4)
    expect(detalle.guia[0].tipExtra).toBe(tips[0])
    // Las semanas que todavía no se celebraron no traen su tip.
    expect(detalle.guia.slice(1).map((g) => g.tipExtra)).toEqual([null, null, null])
  })

  it('un plan que solo tiene tareas semanales se mide en semanas', async () => {
    responderCuestionario('otro')
    const plan = await inscrito(CON_CUESTIONARIO)
    expect(plan.habitos.every((h) => h.periodicidad === 'Semanal')).toBe(true)
    expect(plan.progreso.unidad).toBe('Semanas')

    // Una salida hoy: la semana va "parcial", cuenta para la racha y la tarea sigue pendiente.
    await llamar(`/api/habits/${plan.habitos[0].habitoId}/progress`, {
      metodo: 'POST',
      como: 'otro',
      body: { cantidad: 1, fechaUtc: new Date().toISOString() },
    })
    const { progreso } = await planDe(plan.id, 'otro')
    expect(progreso).toMatchObject({ unidad: 'Semanas', racha: 1, cumplidoHoy: true })
    const tarea = (await hoyDe('otro')).tareas[0]
    expect(tarea).toMatchObject({
      periodicidad: 'Semanal',
      estado: 'Pendiente',
      cantidadPeriodo: 1,
      restante: 2,
    })
  })
})

describe('GET /api/progress/summary y POST /api/progress/achievements/seen', () => {
  const resumenDe = async (como: Quien): Promise<ResumenProgresoDto> =>
    (await llamar('/api/progress/summary', { como })).json()

  it('racha, récord, logros por fecha, el mes en números y los planes en curso', async () => {
    const resumen = await resumenDe('estandar')
    expect(resumen).toMatchObject({ racha: 9, rachaMasLarga: 9, planesEnCurso: 1 })
    expect(resumen.logros.map((l) => [l.clave, l.visto])).toEqual([
      ['PrimerAvance', true],
      ['PrimeraSemana', true],
      ['VeinticincoAvances', true],
    ])
    expect(resumen.logros[0].obtenidoEn).toMatch(/Z$/)
    // El mes local en curso: según el día del mes en que corra la prueba entran más o menos días.
    expect(resumen.mes.diasCumplidos).toBeGreaterThanOrEqual(1)
    expect(resumen.mes.diasCumplidos).toBeLessThanOrEqual(9)
    expect(resumen.mes.regresos).toBe(0)
    expect(resumen.mensaje).toBe((await hoyDe('estandar')).mensaje)
  })

  it('sin planes todo va en cero y no hay mensaje', async () => {
    expect(await resumenDe('otro')).toEqual({
      racha: 0,
      rachaMasLarga: 0,
      logros: [],
      mes: { diasCumplidos: 0, habitosMantenidos: 0, regresos: 0 },
      mensaje: null,
      planesEnCurso: 0,
    })
  })

  it('un logro nuevo llega una vez en Hoy y deja de llegar al marcarlo como visto (204)', async () => {
    const plan = await inscrito('respiracion-anti-estres')
    expect((await hoyDe('otro')).logrosNuevos).toEqual([])
    await llamar(`/api/habits/${plan.habitos[0].habitoId}/progress`, {
      metodo: 'POST',
      como: 'otro',
      body: { cantidad: 5, fechaUtc: new Date().toISOString() },
    })
    expect((await hoyDe('otro')).logrosNuevos).toEqual(['PrimerAvance'])
    // No se gana dos veces.
    expect((await resumenDe('otro')).logros).toEqual([
      { clave: 'PrimerAvance', obtenidoEn: expect.stringMatching(/Z$/), visto: false },
    ])

    const vistos = await llamar('/api/progress/achievements/seen', { metodo: 'POST', como: 'otro' })
    expect(vistos.status).toBe(204)
    expect(await vistos.text()).toBe('')
    expect((await hoyDe('otro')).logrosNuevos).toEqual([])
    expect((await resumenDe('otro')).logros.map((l) => l.visto)).toEqual([true])
    // Los logros de otra persona no se tocan.
    expect(db.logros.filter((l) => l.usuarioId === idDe('estandar'))).toHaveLength(3)
  })

  it('completar un plan gana el logro PlanCompletado', async () => {
    planEnDb(PLAN_DEMO_ID).fechaFinPrevista = new Date(Date.now() - 60_000).toISOString()
    await transicionar(PLAN_DEMO_ID, { nuevoEstado: 'Completado' })
    const resumen = await resumenDe('estandar')
    expect(resumen.planesEnCurso).toBe(0)
    expect(resumen.racha).toBe(0)
    // El plan dura hasta el día anterior a su fecha de fin: hoy ya no cuenta para su récord.
    expect(resumen.rachaMasLarga).toBe(8)
    expect(resumen.logros.find((l) => l.clave === 'PlanCompletado')).toMatchObject({
      visto: false,
    })
  })
})

describe('ResolvedorAncla de la API simulada', () => {
  it('reparte las anclas por la jornada de oficina del perfil', () => {
    const ana = db.perfiles.get(idDe('estandar'))!
    expect(
      (
        [
          'AlDespertar',
          'AlLlegarAlTrabajo',
          'AMediaManana',
          'DespuesDelAlmuerzo',
          'AMediaTarde',
          'AlSalirDelTrabajo',
          'AntesDeCenar',
          'AntesDeDormir',
        ] as const
      ).map((ancla) => resolverAncla(ancla, ana)),
    ).toEqual(['06:30', '08:00', '10:15', '13:00', '14:45', '17:00', '19:30', '22:00'])
  })

  it('el turno de noche envuelve la medianoche y tiene sus horas por defecto', () => {
    const luis = db.perfiles.get(idDe('otro'))!
    expect(resolverAncla('AlDespertar', luis)).toBe('17:00')
    expect(resolverAncla('AMediaManana', luis)).toBe('00:00')
    expect(resolverAncla('DespuesDelAlmuerzo', luis)).toBe('02:30')
    expect(resolverAncla('AntesDeCenar', luis)).toBe('04:30')
  })
})

// --- Las copias del contenido frente a los JSON del backend ---

const CONTENIDO = resolve(
  process.cwd(),
  '..',
  'Modules/HabitTracker/HabitTracker.Infrastructure/Contenido',
)
const RUTA_MENSAJES = resolve(CONTENIDO, 'mensajes-motivacion.json')
const RUTA_CATALOGO = resolve(CONTENIDO, 'catalogo-planes.json')
const hayJson = existsSync(RUTA_MENSAJES) && existsSync(RUTA_CATALOGO)

// Sin el repositorio completo (una imagen que solo copia frontend/) la comparación no se puede hacer.
describe.skipIf(!hayJson)('copias de mensajes-motivacion.json y de los tips del catálogo', () => {
  it('los mensajes de motivación son los del backend', () => {
    const json: { mensajes: { categoria: string; texto: string }[] } = JSON.parse(
      readFileSync(RUTA_MENSAJES, 'utf8'),
    )
    expect(MENSAJES_MOTIVACION).toEqual(
      json.mensajes.map((m) => ({ categoria: m.categoria, texto: m.texto.trim() })),
    )
  })

  it('el tip extra de cada semana es el del catálogo del backend', () => {
    const json: {
      planes: { slug: string; guiaSemanal?: { semana: number; tipExtra?: string }[] }[]
    } = JSON.parse(readFileSync(RUTA_CATALOGO, 'utf8'))
    const esperado = Object.fromEntries(
      json.planes
        .filter((p) => (p.guiaSemanal ?? []).some((g) => g.tipExtra))
        .map((p) => [p.slug, p.guiaSemanal!.map((g) => g.tipExtra ?? null)]),
    )
    expect(TIPS_EXTRA).toEqual(esperado)
  })
})
