import { http, HttpResponse } from 'msw'
import {
  DIAS_MODO_SUAVE,
  DIAS_SIN_REGISTRO_PARA_SUGERIR,
  ESTADOS_PLAN_USUARIO,
  MAXIMO_EXTENSIONES,
  MAXIMO_PLANES_EN_CURSO,
  MOTIVOS_PAUSA,
  TIPOS_AJUSTE,
  type CambiarEstadoPlanRequest,
  type ClaveLogro,
  type CumplimientoDto,
  type EstadoPlanUsuario,
  type EstadoTareaHoy,
  type HoyDto,
  type InscribirseRequest,
  type LogroDto,
  type MesEnNumerosDto,
  type PlanHoyDto,
  type PlanUsuarioDto,
  type ProgresoPlanDto,
  type RecordatorioPlanRequest,
  type ResumenProgresoDto,
  type SugerenciaAjusteDto,
  type TareaHoyDto,
  type TipoAjuste,
  type UnidadSeguimiento,
} from '@/features/mis-planes/types'
import { ANCLAS, type Ancla } from '@/features/planes/types'
import { db, type HabitoMock, type PerfilMock, type PlanUsuarioMock } from '../db'
import { autenticar, esTexto, latencia, leerJson, problema, respuestas } from '../http'
import { MENSAJES_MOTIVACION, TIPS_EXTRA, type CategoriaMensaje } from './misPlanesData'
import { razonesNoApto } from './planes'
import { PLANES_MOCK } from './planesData'
import { sinConsentimientoSalud } from './progreso'

// Réplica de MyPlansController y TodayController (HabitTracker v2, Fase 4) con las reglas de
// PlanUsuarioService, PlanUsuario/TransicionesPlanUsuario, CalculadoraProgreso, SugeridorAjuste,
// CalculadoraLogros, SelectorMensajesMotivacion y ResolvedorAncla. La identidad sale del token y un plan
// de otra persona responde 404 plan_usuario_no_encontrado, como uno que no existe (RD-06).
//
// Simplificaciones respecto al backend:
// - El día local se calcula con Intl y la zona horaria del perfil (UTC sin perfil o con una zona que el
//   navegador no conoce). En una zona con cambio de hora puede diferir un día de TimeZoneInfo justo en el
//   salto; el resto de CalculadoraProgreso (racha tolerante, pausas, modo suave, planes semanales,
//   semanas celebradas) está portado regla por regla.
// - No hay índice único: dos inscripciones simultáneas al mismo plan no se simulan (el navegador de la
//   demo es uno solo).
// - Un plan retirado del catálogo no existe aquí: todos los de planesData.ts están activos.

const ROLES = ['Estandar' as const]
const DIA_MS = 86_400_000
const DIAS_POR_SEMANA = 7
// CalculadoraProgreso: dos días (o semanas) seguidos sin registro cortan la racha; 5 de 7 celebran la semana.
const SEGUIDOS_QUE_CORTAN = 2
const CUMPLIDOS_PARA_CELEBRAR = 5
const SEMANAS_VENTANA_LARGA = 4

const esGuid = (v: unknown): v is string =>
  typeof v === 'string' && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(v)
const esUno = <T extends string>(lista: readonly T[], v: unknown): v is T =>
  typeof v === 'string' && (lista as readonly string[]).includes(v)
const nuloO = (v: unknown, valido: (v: unknown) => boolean) =>
  v === null || v === undefined || valido(v)
// TimeOnly de System.Text.Json: "HH:mm" o "HH:mm:ss".
const esHora = (v: unknown): v is string =>
  typeof v === 'string' && /^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$/.test(v)

// ArgumentException del servicio o del dominio: 400 sin código (GlobalExceptionMiddleware).
const argumentoInvalido = () =>
  problema(400, 'Solicitud inválida.', 'La solicitud contiene datos no válidos.')
const planUsuarioNoEncontrado = () =>
  problema(404, 'No encontrado.', 'No encontramos ese plan.', 'plan_usuario_no_encontrado')

// --- Día local (CalculadoraProgreso.DiaLocal) ---

const formatos = new Map<string, Intl.DateTimeFormat | null>()

function formatoDe(zona: string): Intl.DateTimeFormat | null {
  if (!formatos.has(zona)) {
    try {
      formatos.set(
        zona,
        new Intl.DateTimeFormat('en-CA', {
          timeZone: zona,
          year: 'numeric',
          month: '2-digit',
          day: '2-digit',
        }),
      )
    } catch {
      // Seguimiento.Zona: una zona desconocida no rompe el plan; se usa UTC.
      formatos.set(zona, null)
    }
  }
  return formatos.get(zona) ?? null
}

// Número de día (días desde 1970-01-01) del día local al que pertenece el instante.
function diaLocal(zona: string, instante: number): number {
  const formato = zona === 'UTC' ? null : formatoDe(zona)
  if (!formato) return Math.floor(instante / DIA_MS)
  const partes = formato.formatToParts(new Date(instante))
  const parte = (tipo: string) => Number(partes.find((p) => p.type === tipo)?.value)
  return Date.UTC(parte('year'), parte('month') - 1, parte('day')) / DIA_MS
}

const zonaDe = (usuarioId: string) => db.perfiles.get(usuarioId)?.zonaHoraria ?? 'UTC'
// 1970-01-01 fue jueves. Lunes de la semana (lunes a domingo) del día.
const inicioDeSemana = (dia: number) => dia - ((((dia + 4) % 7) + 6) % 7)
const fechaDe = (dia: number) => new Date(dia * DIA_MS)
const textoDeDia = (dia: number) => fechaDe(dia).toISOString().slice(0, 10)

// --- ResolvedorAncla: del ancla a una hora local "HH:mm" según la jornada del perfil ---

const MINUTOS_DIA = 24 * 60
const envolver = (minutos: number) => ((minutos % MINUTOS_DIA) + MINUTOS_DIA) % MINUTOS_DIA

function minutosDe(hora: string | null): number | null {
  if (!hora) return null
  const [h, m, s = '0'] = hora.split(':')
  return Number(h) * 60 + Number(m) + Number(s) / 60
}

function horaTexto(minutos: number): string {
  const total = Math.floor(envolver(minutos))
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}

export function resolverAncla(ancla: Ancla, perfil: PerfilMock): string {
  const nocturna =
    perfil.perfilLaboral === 'TurnoNocturno' || perfil.perfilLaboral === 'TurnosRotativos'
  const entrada = minutosDe(perfil.horaEntradaTrabajo) ?? (nocturna ? 22 * 60 : 8 * 60)
  const salida = minutosDe(perfil.horaSalidaTrabajo) ?? (nocturna ? 6 * 60 : 17 * 60)
  const despertar = minutosDe(perfil.horaDespertar) ?? (nocturna ? 17 * 60 : 6 * 60 + 30)
  const dormir = minutosDe(perfil.horaDormir) ?? (nocturna ? 7 * 60 + 30 : 22 * 60 + 30)
  // De 22:00 a 06:00 son 8 h; una entrada igual a la salida se toma como 8 h.
  const duracion = envolver(salida - entrada) || 8 * 60

  switch (ancla) {
    case 'AlDespertar':
      return horaTexto(despertar)
    case 'AlLlegarAlTrabajo':
      return horaTexto(entrada)
    case 'AMediaManana':
      return horaTexto(entrada + duracion * 0.25)
    case 'DespuesDelAlmuerzo':
      return horaTexto(entrada + duracion * 0.5 + 30)
    case 'AMediaTarde':
      return horaTexto(entrada + duracion * 0.75)
    case 'AlSalirDelTrabajo':
      return horaTexto(salida)
    case 'AntesDeCenar':
      return horaTexto(dormir - 3 * 60)
    case 'AntesDeDormir':
      return horaTexto(dormir - 30)
  }
}

// Seguimiento.Hora: sin perfil o sin ancla no hay hora sugerida.
const horaSugerida = (ancla: Ancla | undefined, perfil: PerfilMock | undefined) =>
  ancla && perfil ? resolverAncla(ancla, perfil) : null

// --- CalculadoraProgreso ---

type EstadoDia = 'SinRegistro' | 'Parcial' | 'Minimo' | 'Completo' | 'EnPausa'

interface Dia {
  dia: number
  estado: EstadoDia
}

interface Cumplimiento {
  diasCompletos: number
  diasMinimos: number
  diasElegibles: number
}

// ResultadoProgreso: lo que viaja en ProgresoPlanDto más lo que usan la sugerencia, los logros y el mensaje.
interface Progreso {
  hoy: number
  racha: number
  rachaMasLarga: number
  siete: Cumplimiento
  treinta: Cumplimiento
  semanaActual: number
  semanasTotales: number
  diasRestantes: number
  diasTranscurridos: number
  diasCumplidos: number
  diasSinRegistroSeguidos: number
  diasSinRegistroUltimos7: number
  cumplidoHoy: boolean
  semanasCelebradas: number[]
  // Días en que la persona volvió tras 2 días (o 2 semanas) sin registro o más.
  regresos: number[]
  unidad: UnidadSeguimiento
}

const esCumplido = (estado: EstadoDia) =>
  estado === 'Parcial' || estado === 'Minimo' || estado === 'Completo'

const semanasTotalesDe = (plan: PlanUsuarioMock) => plan.semanas + plan.extensiones
const enModoSuave = (plan: PlanUsuarioMock, ahora: number) =>
  plan.modoSuaveHasta !== null && ahora <= Date.parse(plan.modoSuaveHasta)
const llegoFechaFin = (plan: PlanUsuarioMock, ahora: number) =>
  ahora >= Date.parse(plan.fechaFinPrevista)
const estaEnCurso = (plan: PlanUsuarioMock) => plan.estado === 'Activo' || plan.estado === 'Pausado'

// La racha tolerante, igual para días y para semanas: una unidad sin registro no la corta y dos seguidas
// sí. La unidad en curso (hoy, o esta semana) todavía no se da por fallada.
function recorrerRacha(unidades: { estado: EstadoDia; enCurso: boolean; diaDeRegreso: number }[]) {
  let racha = 0
  let rachaMasLarga = 0
  let hueco = 0
  let cumplidos = 0
  let haCumplidoAntes = false
  const regresos: number[] = []
  for (const unidad of unidades) {
    if (esCumplido(unidad.estado)) {
      if (hueco >= SEGUIDOS_QUE_CORTAN) {
        racha = 1
        if (haCumplidoAntes) regresos.push(unidad.diaDeRegreso)
      } else {
        racha++
      }
      hueco = 0
      cumplidos++
      haCumplidoAntes = true
      rachaMasLarga = Math.max(rachaMasLarga, racha)
    } else if (!unidad.enCurso) {
      hueco++
    }
  }
  if (hueco >= SEGUIDOS_QUE_CORTAN) racha = 0
  return { racha, rachaMasLarga, hueco, cumplidos, regresos }
}

function calcularProgreso(
  plan: PlanUsuarioMock,
  habitos: HabitoMock[],
  zona: string,
  ahora: number,
): Progreso {
  const dia = (iso: string) => diaLocal(zona, Date.parse(iso))
  // Cuentan las tareas que la persona sigue: las activas y las que el plan pausó con él (Seguimiento.Progreso).
  const tareas = habitos.filter(
    (h) => h.estado === 'Activo' || (h.estado === 'Pausado' && h.pausadoPorPlan),
  )
  const registros = habitos.flatMap((h) =>
    h.registros.map((r) => ({
      habitoId: h.id,
      dia: dia(r.fecha),
      minima: r.esVersionMinima === true,
    })),
  )

  const hoy = diaLocal(zona, ahora)
  const inicio = dia(plan.fechaInicio)
  const finPlan = dia(plan.fechaFinPrevista)
  // El plan dura hasta el día anterior a la fecha de fin prevista (4 semanas = 28 días, no 29).
  const ultimo = Math.max(inicio, Math.min(hoy, finPlan - 1))
  const semanasTotales = semanasTotalesDe(plan)

  const diarias = new Set(tareas.filter((t) => t.periodicidad === 'Diaria').map((t) => t.id))
  const pausas = plan.pausas.map((x) => ({
    inicio: dia(x.inicioUtc),
    fin: x.finUtc === null ? null : dia(x.finUtc),
  }))
  // Una pausa cubre del día en que empezó a antes del día en que se reanudó; abierta, hasta hoy.
  const enPausa = (d: number) =>
    pausas.some((x) => d >= x.inicio && (x.fin === null ? d <= hoy : d < x.fin))
  const suaveHasta = plan.modoSuaveHasta === null ? null : Date.parse(plan.modoSuaveHasta)
  const modoSuave = (d: number) =>
    suaveHasta !== null &&
    d <= diaLocal(zona, suaveHasta) &&
    d >= diaLocal(zona, suaveHasta - DIAS_MODO_SUAVE * DIA_MS)

  const estadoDelDia = (d: number): EstadoDia => {
    const delDia = registros.filter((r) => r.dia === d)
    // Un día con avance se evalúa como cualquier otro, aunque el plan estuviera en pausa.
    if (delDia.length === 0) return enPausa(d) ? 'EnPausa' : 'SinRegistro'
    const suave = modoSuave(d)
    // Un plan sin tareas diarias: cualquier avance cumple el día.
    if (diarias.size === 0) return delDia.every((r) => r.minima) && !suave ? 'Minimo' : 'Completo'

    let todasConAvance = true
    let todasCompletas = true
    for (const tarea of diarias) {
      const deLaTarea = delDia.filter((r) => r.habitoId === tarea)
      if (deLaTarea.length === 0) {
        todasConAvance = false
        todasCompletas = false
      } else if (deLaTarea.every((r) => r.minima)) {
        todasCompletas = false
      }
    }
    if (!todasConAvance) return 'Parcial'
    return todasCompletas || suave ? 'Completo' : 'Minimo'
  }

  const dias: Dia[] = []
  for (let d = inicio; d <= ultimo; d++) dias.push({ dia: d, estado: estadoDelDia(d) })
  // Los días de una pausa sin avance no cuentan ni avanzan el programa.
  const activos = dias.filter((x) => x.estado !== 'EnPausa')
  const ultimoDia = dias[dias.length - 1]
  const comun = {
    hoy,
    semanaActual: Math.min(
      Math.max(Math.trunc((activos.length - 1) / DIAS_POR_SEMANA) + 1, 1),
      Math.max(1, semanasTotales),
    ),
    semanasTotales,
    diasRestantes: Math.max(0, finPlan - hoy),
    diasTranscurridos: activos.length,
    cumplidoHoy: ultimoDia.dia === hoy && esCumplido(ultimoDia.estado),
  }

  const resumir = (ventana: Dia[]): Cumplimiento => {
    // Hoy solo cuenta si ya tiene avance: sigue en curso.
    const elegibles = ventana.filter(
      (x) => x.estado !== 'EnPausa' && !(x.dia === hoy && x.estado === 'SinRegistro'),
    )
    return {
      diasCompletos: elegibles.filter((x) => x.estado === 'Completo').length,
      diasMinimos: elegibles.filter((x) => x.estado === 'Minimo').length,
      diasElegibles: elegibles.length,
    }
  }

  // Un plan que solo tiene tareas semanales (tres salidas a correr por semana) se mide en semanas.
  if (tareas.length > 0 && diarias.size === 0) {
    // La meta de la semana: las sesiones de la tarea más exigente.
    const meta = Math.min(
      Math.max(Math.ceil(Math.max(...tareas.map((t) => t.cantidadObjetivo))), 1),
      DIAS_POR_SEMANA,
    )
    const porSemana = new Map<number, Dia[]>()
    for (const x of dias) {
      const lunes = inicioDeSemana(x.dia)
      porSemana.set(lunes, [...(porSemana.get(lunes) ?? []), x])
    }
    const semanas = [...porSemana.entries()].map(([lunes, deLaSemana]) => {
      const completas = deLaSemana.filter((x) => x.estado === 'Completo').length
      const conAlgo = deLaSemana.filter((x) => esCumplido(x.estado)).length
      const estado: EstadoDia = deLaSemana.every((x) => x.estado === 'EnPausa')
        ? 'EnPausa'
        : completas >= meta
          ? 'Completo'
          : conAlgo >= meta
            ? 'Minimo'
            : conAlgo >= 1
              ? 'Parcial'
              : 'SinRegistro'
      return { lunes, estado, dias: deLaSemana }
    })

    const semanaEnCurso = inicioDeSemana(hoy)
    const { racha, rachaMasLarga, regresos } = recorrerRacha(
      semanas
        .filter((s) => s.estado !== 'EnPausa')
        .map((s) => ({
          estado: s.estado,
          enCurso: s.lunes === semanaEnCurso,
          diaDeRegreso: s.dias.find((x) => esCumplido(x.estado))?.dia ?? s.lunes,
        })),
    )

    // Solo cuenta una semana entera (o más) sin una sola sesión, desde la última o desde el inicio.
    const conAvance = activos.filter((x) => esCumplido(x.estado))
    const diasDesdeLaUltima = hoy - (conAvance[conAvance.length - 1]?.dia ?? inicio)
    const semanaSinSesion = diasDesdeLaUltima >= DIAS_POR_SEMANA

    const resumirSemanas = (ultimas: number): Cumplimiento => {
      const elegibles = semanas
        .filter(
          (s) =>
            s.estado !== 'EnPausa' && !(s.lunes === semanaEnCurso && s.estado === 'SinRegistro'),
        )
        .slice(-ultimas)
      return {
        diasCompletos: elegibles.filter((s) => s.estado === 'Completo').length,
        diasMinimos: elegibles.filter((s) => s.estado === 'Minimo').length,
        diasElegibles: elegibles.length,
      }
    }

    return {
      ...comun,
      racha,
      rachaMasLarga,
      siete: resumirSemanas(1),
      treinta: resumirSemanas(SEMANAS_VENTANA_LARGA),
      diasCumplidos: conAvance.length,
      diasSinRegistroSeguidos: semanaSinSesion ? diasDesdeLaUltima : 0,
      diasSinRegistroUltimos7: semanaSinSesion ? DIAS_POR_SEMANA : 0,
      // La semana w del plan es la w-ésima de calendario; se celebra pasado su domingo con la meta cumplida.
      semanasCelebradas: semanas
        .slice(0, semanasTotales)
        .flatMap((s, i) =>
          s.lunes + DIAS_POR_SEMANA - 1 < hoy && s.estado === 'Completo' ? [i + 1] : [],
        ),
      regresos,
      unidad: 'Semanas',
    }
  }

  const { racha, rachaMasLarga, hueco, cumplidos, regresos } = recorrerRacha(
    activos.map((x) => ({ estado: x.estado, enCurso: x.dia === hoy, diaDeRegreso: x.dia })),
  )
  const ultimosSiete = dias.filter((x) => x.dia > ultimo - 7)

  // Las semanas del plan se cuentan en días que cuentan; una semana cierra al empezar la siguiente.
  const semanasCelebradas: number[] = []
  for (let semana = 1; semana <= semanasTotales; semana++) {
    const hasta = semana * DIAS_POR_SEMANA
    const cerrada = hasta < activos.length || (hasta === activos.length && hoy >= finPlan)
    if (!cerrada) break
    const cumplidosDeLaSemana = activos
      .slice(hasta - DIAS_POR_SEMANA, hasta)
      .filter((x) => esCumplido(x.estado)).length
    if (cumplidosDeLaSemana >= CUMPLIDOS_PARA_CELEBRAR) semanasCelebradas.push(semana)
  }

  return {
    ...comun,
    racha,
    rachaMasLarga,
    siete: resumir(ultimosSiete),
    treinta: resumir(dias.filter((x) => x.dia > ultimo - 30)),
    diasCumplidos: cumplidos,
    diasSinRegistroSeguidos: hueco,
    diasSinRegistroUltimos7: ultimosSiete.filter((x) => x.estado === 'SinRegistro' && x.dia !== hoy)
      .length,
    semanasCelebradas,
    regresos,
    unidad: 'Dias',
  }
}

// Math.Round de decimal: a un decimal y con el punto medio hacia el par.
function redondear1(valor: number): number {
  const x = valor * 10
  const piso = Math.floor(x)
  if (Math.abs(x - piso - 0.5) < 1e-9) return (piso % 2 === 0 ? piso : piso + 1) / 10
  return Math.round(x) / 10
}

function cumplimientoDto(c: Cumplimiento): CumplimientoDto {
  const porcentaje = (unidades: number) =>
    c.diasElegibles === 0 ? 0 : redondear1((unidades * 100) / c.diasElegibles)
  return {
    ...c,
    porcentajeCompleto: porcentaje(c.diasCompletos),
    porcentajeConMinimo: porcentaje(c.diasCompletos + c.diasMinimos),
  }
}

const progresoDto = (p: Progreso): ProgresoPlanDto => ({
  racha: p.racha,
  rachaMasLarga: p.rachaMasLarga,
  siete: cumplimientoDto(p.siete),
  treinta: cumplimientoDto(p.treinta),
  semanaActual: p.semanaActual,
  semanasTotales: p.semanasTotales,
  diasRestantes: p.diasRestantes,
  diasCumplidos: p.diasCumplidos,
  diasSinRegistroSeguidos: p.diasSinRegistroSeguidos,
  cumplidoHoy: p.cumplidoHoy,
  semanasCelebradas: p.semanasCelebradas,
  unidad: p.unidad,
})

// SugeridorAjuste: con 4 o más días sin registro de los últimos 7 se ofrece ajustar. Solo propone.
function sugerir(
  progreso: Progreso,
  plan: PlanUsuarioMock,
  ahora: number,
): SugerenciaAjusteDto | null {
  if (plan.estado !== 'Activo' || progreso.diasSinRegistroUltimos7 < DIAS_SIN_REGISTRO_PARA_SUGERIR)
    return null
  const opciones: TipoAjuste[] = []
  if (!enModoSuave(plan, ahora)) opciones.push('ModoSuave')
  if (plan.extensiones < MAXIMO_EXTENSIONES) opciones.push('Extender')
  opciones.push('Pausar')
  return { opciones, diasSinRegistro: progreso.diasSinRegistroUltimos7 }
}

// --- De la base simulada a los DTO (Seguimiento.ADto) ---

const habitosDe = (plan: PlanUsuarioMock) =>
  db.habitos.filter((h) => h.usuarioId === plan.usuarioId && h.planUsuarioId === plan.id)
const plantillaDe = (plan: PlanUsuarioMock) =>
  plan.slug === null ? undefined : PLANES_MOCK.find((p) => p.slug === plan.slug)

function planADto(plan: PlanUsuarioMock, ahora = Date.now()): PlanUsuarioDto {
  const perfil = db.perfiles.get(plan.usuarioId)
  const delPlan = habitosDe(plan)
  const progreso = calcularProgreso(plan, delPlan, zonaDe(plan.usuarioId), ahora)
  const tips = plan.slug === null ? undefined : TIPS_EXTRA[plan.slug]
  return {
    id: plan.id,
    slug: plan.slug,
    nombre: plan.nombre,
    origen: plan.origen,
    estado: plan.estado,
    motivoPausa: plan.motivoPausa,
    semanas: plan.semanas,
    semanasTotales: semanasTotalesDe(plan),
    extensiones: plan.extensiones,
    fechaInicio: plan.fechaInicio,
    fechaFinPrevista: plan.fechaFinPrevista,
    modoSuaveHasta: plan.modoSuaveHasta,
    horaRecordatorio: plan.horaRecordatorio,
    recordatoriosActivos: plan.recordatoriosActivos,
    habitos: delPlan.map((h) => ({
      habitoId: h.id,
      nombre: h.nombre,
      descripcion: h.descripcion ?? null,
      periodicidad: h.periodicidad,
      cantidadObjetivo: h.cantidadObjetivo,
      unidad: h.unidad,
      estado: h.estado,
      versionMinima: h.versionMinima ?? null,
      ancla: h.ancla ?? null,
      horaSugerida: horaSugerida(h.ancla, perfil),
    })),
    progreso: progresoDto(progreso),
    sugerencia: sugerir(progreso, plan, ahora),
    // El tip extra solo viaja en las semanas que la persona ya celebró.
    guia: (plantillaDe(plan)?.guiaSemanal ?? []).map((g) => ({
      semana: g.semana,
      texto: g.texto,
      tipExtra: progreso.semanasCelebradas.includes(g.semana)
        ? (tips?.[g.semana - 1] ?? null)
        : null,
    })),
  }
}

// PlanUsuarioRepository.ListarDeUsuarioAsync: del más reciente al más antiguo.
const planesDe = (usuarioId: string) =>
  db.planesUsuario
    .filter((p) => p.usuarioId === usuarioId)
    .sort((a, b) => Date.parse(b.fechaInicio) - Date.parse(a.fechaInicio))

interface ProgresoDePlan {
  plan: PlanUsuarioMock
  habitos: HabitoMock[]
  progreso: Progreso
}

function progresosDe(usuarioId: string, ahora: number): ProgresoDePlan[] {
  const zona = zonaDe(usuarioId)
  return planesDe(usuarioId).map((plan) => {
    const habitos = habitosDe(plan)
    return { plan, habitos, progreso: calcularProgreso(plan, habitos, zona, ahora) }
  })
}

const porInicio = (a: ProgresoDePlan, b: ProgresoDePlan) =>
  Date.parse(a.plan.fechaInicio) - Date.parse(b.plan.fechaInicio)

// --- CalculadoraLogros + LogrosService: se ganan al consultar Hoy o el resumen, una sola vez cada uno ---

function sincronizarLogros(usuarioId: string, progresos: ProgresoDePlan[], ahora: number) {
  const habitos = db.habitos.filter((h) => h.usuarioId === usuarioId)
  const avances = habitos.reduce((total, h) => total + h.registros.length, 0)
  const rachaMasLarga = Math.max(0, ...progresos.map((p) => p.progreso.rachaMasLarga))
  const condiciones: [ClaveLogro, boolean][] = [
    ['PrimerAvance', avances >= 1],
    ['PrimeraSemana', rachaMasLarga >= 7],
    ['VeinticincoAvances', avances >= 25],
    ['Constancia21', rachaMasLarga >= 21],
    ['Constancia30', rachaMasLarga >= 30],
    ['MetaCumplida', habitos.some((h) => h.estado === 'Completado')],
    ['Regreso', progresos.some((p) => p.progreso.regresos.length > 0)],
    [
      'MitadDelPlan',
      progresos.some(
        (p) =>
          semanasTotalesDe(p.plan) > 0 &&
          p.progreso.diasTranscurridos * 2 >= semanasTotalesDe(p.plan) * DIAS_POR_SEMANA,
      ),
    ],
    ['PlanCompletado', progresos.some((p) => p.plan.estado === 'Completado')],
  ]
  const propios = () => db.logros.filter((l) => l.usuarioId === usuarioId)
  const obtenidos = new Set(propios().map((l) => l.clave))
  for (const [clave, ganado] of condiciones) {
    if (ganado && !obtenidos.has(clave))
      db.logros.push({
        usuarioId,
        clave,
        obtenidoEn: new Date(ahora).toISOString(),
        vistoEn: null,
      })
  }
  return propios()
}

// --- SelectorMensajesMotivacion: un mensaje al día, según el momento de la persona ---

function categorizar(p: Progreso, planTerminado: boolean): CategoriaMensaje {
  if (planTerminado) return 'Cierre'
  if (p.diasSinRegistroSeguidos >= SEGUIDOS_QUE_CORTAN) return 'Regreso'
  if (p.regresos.includes(p.hoy)) return 'Recaida'
  const esLaMitad =
    p.semanasTotales > 0 &&
    p.diasTranscurridos === Math.trunc((p.semanasTotales * DIAS_POR_SEMANA + 1) / 2)
  if ([7, 21, 30].includes(p.racha) || esLaMitad) return 'Hito'
  return p.diasTranscurridos <= 2 ? 'Inicio' : 'Constancia'
}

// FNV-1a sobre los bytes del Guid en el orden de Guid.ToByteArray() (los tres primeros grupos, invertidos).
function hashEstable(id: string): number {
  const hex = id.replace(/-/g, '')
  const orden = [3, 2, 1, 0, 5, 4, 7, 6, 8, 9, 10, 11, 12, 13, 14, 15]
  let hash = 2166136261
  for (const i of orden)
    hash = Math.imul(hash ^ Number.parseInt(hex.slice(i * 2, i * 2 + 2), 16), 16777619) >>> 0
  return hash
}

// DateOnly.DayNumber cuenta desde el 0001-01-01.
const DIAS_HASTA_1970 = 719_162

// Determinista: el mismo día da siempre el mismo mensaje y el siguiente, otro de la misma categoría.
function mensajeDelDia(principal: ProgresoDePlan | undefined, ahora: number): string | null {
  if (!principal) return null
  const categoria = categorizar(principal.progreso, llegoFechaFin(principal.plan, ahora))
  const textos = MENSAJES_MOTIVACION.filter((m) => m.categoria === categoria).map((m) => m.texto)
  if (textos.length === 0) return null
  const indice =
    (hashEstable(principal.plan.usuarioId) + principal.progreso.hoy + DIAS_HASTA_1970) %
    textos.length
  return textos[indice]
}

// --- Hoy (HoyService) ---

function tareaDeHoy(
  plan: PlanUsuarioMock,
  habito: HabitoMock,
  hoy: number,
  zona: string,
  perfil: PerfilMock | undefined,
): TareaHoyDto {
  // El periodo de una tarea diaria es el día local; el de una semanal, la semana local (lunes a domingo).
  const desde = habito.periodicidad === 'Semanal' ? inicioDeSemana(hoy) : hoy
  const delPeriodo = habito.registros.filter((r) => {
    const d = diaLocal(zona, Date.parse(r.fecha))
    return d >= desde && d <= hoy
  })
  // decimal en el backend: sin el ruido de la coma flotante.
  const cantidad = Math.round(delPeriodo.reduce((total, r) => total + r.cantidad, 0) * 1e4) / 1e4
  const estado: EstadoTareaHoy =
    cantidad >= habito.cantidadObjetivo
      ? 'Hecho'
      : delPeriodo.length > 0 && delPeriodo.every((r) => r.esVersionMinima === true)
        ? 'Minimo'
        : 'Pendiente'
  return {
    habitoId: habito.id,
    planId: plan.id,
    planNombre: plan.nombre,
    nombre: habito.nombre,
    instrucciones: habito.descripcion ?? null,
    periodicidad: habito.periodicidad,
    cantidadObjetivo: habito.cantidadObjetivo,
    unidad: habito.unidad,
    ancla: habito.ancla ?? null,
    horaSugerida: horaSugerida(habito.ancla, perfil),
    versionMinima: habito.versionMinima ?? null,
    estado,
    cantidadPeriodo: cantidad,
    restante: Math.max(0, Math.round((habito.cantidadObjetivo - cantidad) * 1e4) / 1e4),
  }
}

function hoyDe(usuarioId: string, ahora = Date.now()): HoyDto {
  const perfil = db.perfiles.get(usuarioId)
  const zona = zonaDe(usuarioId)
  const hoy = diaLocal(zona, ahora)
  const progresos = progresosDe(usuarioId, ahora)
  const activos = progresos.filter((p) => p.plan.estado === 'Activo').sort(porInicio)
  const logros = sincronizarLogros(usuarioId, progresos, ahora)

  // La semana recién cerrada, si se celebró, es la anterior a la actual: ahí va su tip extra.
  const principal = activos[0]
  const semanaCerrada = principal ? principal.progreso.semanaActual - 1 : 0
  const hayCelebracion =
    semanaCerrada >= 1 && principal.progreso.semanasCelebradas.includes(semanaCerrada)
  const tips = principal?.plan.slug ? TIPS_EXTRA[principal.plan.slug] : undefined

  return {
    fecha: textoDeDia(hoy),
    tareas: activos.flatMap((p) =>
      p.habitos
        .filter((h) => h.estado === 'Activo')
        .map((h) => tareaDeHoy(p.plan, h, hoy, zona, perfil)),
    ),
    planes: activos.map((p): PlanHoyDto => ({
      planId: p.plan.id,
      nombre: p.plan.nombre,
      semanaActual: p.progreso.semanaActual,
      semanasTotales: p.progreso.semanasTotales,
      diasRestantes: p.progreso.diasRestantes,
      racha: p.progreso.racha,
      sugerencia: sugerir(p.progreso, p.plan, ahora),
    })),
    mensaje: mensajeDelDia(principal, ahora),
    celebracionSemana: hayCelebracion ? semanaCerrada : null,
    tipExtra: hayCelebracion ? (tips?.[semanaCerrada - 1] ?? null) : null,
    logrosNuevos: logros.filter((l) => l.vistoEn === null).map((l) => l.clave),
  }
}

// --- Resumen de constancia (ResumenProgresoService) ---

const DIAS_DEL_MES_PARA_MANTENER = 3

function resumenDe(usuarioId: string, ahora = Date.now()): ResumenProgresoDto {
  const zona = zonaDe(usuarioId)
  const hoy = diaLocal(zona, ahora)
  const progresos = progresosDe(usuarioId, ahora)
  const enCurso = progresos.filter((p) => estaEnCurso(p.plan))
  const logros = sincronizarLogros(usuarioId, progresos, ahora)
  const principal = enCurso.filter((p) => p.plan.estado === 'Activo').sort(porInicio)[0]

  // El mes local en curso, solo con los hábitos de los planes.
  const esDelMes = (d: number) =>
    fechaDe(d).getUTCFullYear() === fechaDe(hoy).getUTCFullYear() &&
    fechaDe(d).getUTCMonth() === fechaDe(hoy).getUTCMonth()
  const deLosPlanes = db.habitos
    .filter((h) => h.usuarioId === usuarioId && h.planUsuarioId !== undefined)
    .map((h) => ({
      estado: h.estado,
      dias: new Set(h.registros.map((r) => diaLocal(zona, Date.parse(r.fecha))).filter(esDelMes)),
    }))
  const mes: MesEnNumerosDto = {
    diasCumplidos: new Set(deLosPlanes.flatMap((h) => [...h.dias])).size,
    habitosMantenidos: deLosPlanes.filter(
      (h) => h.estado === 'Activo' && h.dias.size >= DIAS_DEL_MES_PARA_MANTENER,
    ).length,
    regresos: progresos.reduce(
      (total, p) => total + p.progreso.regresos.filter(esDelMes).length,
      0,
    ),
  }

  return {
    racha: Math.max(0, ...enCurso.map((p) => p.progreso.racha)),
    rachaMasLarga: Math.max(0, ...progresos.map((p) => p.progreso.rachaMasLarga)),
    logros: [...logros]
      .sort((a, b) => Date.parse(a.obtenidoEn) - Date.parse(b.obtenidoEn))
      .map((l): LogroDto => ({
        clave: l.clave,
        obtenidoEn: l.obtenidoEn,
        visto: l.vistoEn !== null,
      })),
    mes,
    mensaje: mensajeDelDia(principal, ahora),
    planesEnCurso: enCurso.length,
  }
}

// --- Máquina de estados del plan (TransicionesPlanUsuario) ---

// Prohibidas explícitas: Pausado → Completado y salir de un estado terminal. Completar exige haber
// llegado a la fecha de fin (plan_aun_no_termina).
function transicionRechazada(
  actual: EstadoPlanUsuario,
  nuevo: EstadoPlanUsuario,
  llegoFin: boolean,
): Response | null {
  const invalida = (detalle: string) => respuestas.codigo('transicion_plan_invalida', detalle)
  if (actual === 'Completado' || actual === 'Cancelado')
    return invalida('Un plan completado o cancelado ya no cambia.')
  if (actual === 'Pausado' && nuevo === 'Completado')
    return invalida('Primero reanuda el plan; un plan en pausa no se puede completar.')
  const valida =
    actual === 'Activo'
      ? nuevo === 'Pausado' || nuevo === 'Cancelado' || (nuevo === 'Completado' && llegoFin)
      : nuevo === 'Activo' || nuevo === 'Cancelado'
  if (valida) return null
  if (actual === 'Activo' && nuevo === 'Completado')
    return respuestas.codigo('plan_aun_no_termina', 'Todavía no llegaste al final del plan.')
  return invalida(`No se puede pasar de ${actual} a ${nuevo}.`)
}

// Habito.PausarPorPlan: el plan se pausa (o termina sin conservar los hábitos) y pausa sus hábitos activos.
function pausarHabitos(habitos: HabitoMock[]) {
  for (const habito of habitos.filter((h) => h.estado === 'Activo')) {
    habito.estado = 'Pausado'
    habito.pausadoPorPlan = true
  }
}

function planPropio(id: string, usuarioId: string): PlanUsuarioMock | Response {
  // La ruta del backend es {id:guid}: un id que no es GUID no llega al controller (404 sin cuerpo).
  if (!esGuid(id)) return new HttpResponse(null, { status: 404 })
  const plan = db.planesUsuario.find((p) => p.id === id.toLowerCase())
  // Un plan de otra persona responde igual que uno que no existe: no se revela que existe.
  return plan && plan.usuarioId === usuarioId ? plan : planUsuarioNoEncontrado()
}

export const misPlanesHandlers = [
  http.post('/api/my-plans', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const usuarioId = sesion.usuario.id
    const body = await leerJson<InscribirseRequest>(request)
    if (
      !body ||
      !esTexto(body.slug, 1, 60) ||
      !nuloO(body.ancla, (v) => esUno(ANCLAS, v)) ||
      !nuloO(body.hora, esHora)
    )
      return respuestas.invalida()

    const perfil = db.perfiles.get(usuarioId)
    if (!perfil)
      return problema(
        404,
        'No encontrado.',
        'Aún no completaste tu perfil.',
        'perfil_no_encontrado',
      )
    const plantilla = PLANES_MOCK.find((p) => p.slug === body.slug)
    if (!plantilla)
      return problema(404, 'No encontrado.', 'No encontramos ese plan.', 'plan_no_encontrado')

    // P1: un Básico sin requisitos de salud no pide el consentimiento ni el cuestionario.
    const sinConsentimiento = sinConsentimientoSalud(usuarioId)
    if (plantilla.requiereCuestionario && sinConsentimiento) return sinConsentimiento

    // EvaluadorSalud.BloquesFaltantesPara: solo los bloques que el plan necesita.
    const cuestionario = sinConsentimiento ? undefined : db.cuestionarios.get(usuarioId)
    const faltanBloques =
      (plantilla.requiereCuestionario && (!cuestionario?.objetivos || !cuestionario.seguridad)) ||
      ((plantilla.contraindicaciones.length > 0 || plantilla.alergenos.length > 0) &&
        !cuestionario?.condiciones)
    if (faltanBloques)
      return respuestas.codigo(
        'cuestionario_incompleto',
        'Faltan respuestas de tu cuestionario de salud para empezar este plan.',
      )

    const razones = razonesNoApto(usuarioId, perfil, plantilla)
    if (razones.length > 0) return respuestas.codigo('plan_no_apto', razones[0].mensaje)

    const enCurso = planesDe(usuarioId).filter(estaEnCurso)
    if (enCurso.some((p) => p.slug === plantilla.slug))
      return respuestas.codigo('plan_ya_activo', 'Ya estás siguiendo este plan.')
    if (enCurso.length >= MAXIMO_PLANES_EN_CURSO)
      return respuestas.codigo(
        'limite_planes',
        `Puedes seguir ${MAXIMO_PLANES_EN_CURSO} planes a la vez. Termina o cancela uno para empezar otro.`,
      )

    const ahora = Date.now()
    const inicio = new Date(ahora).toISOString()
    const plan: PlanUsuarioMock = {
      id: crypto.randomUUID(),
      usuarioId,
      slug: plantilla.slug,
      nombre: plantilla.nombre,
      origen: 'Catalogo',
      estado: 'Activo',
      motivoPausa: null,
      semanas: plantilla.semanas,
      extensiones: 0,
      fechaInicio: inicio,
      fechaFinPrevista: new Date(
        ahora + plantilla.semanas * DIAS_POR_SEMANA * DIA_MS,
      ).toISOString(),
      modoSuaveHasta: null,
      // La hora manda sobre el ancla; sin ninguna, el ancla de la primera tarea.
      horaRecordatorio: body.hora
        ? body.hora.slice(0, 5)
        : resolverAncla(body.ancla ?? plantilla.tareas[0].anclaSugerida, perfil),
      recordatoriosActivos: false,
      pausas: [],
    }
    db.planesUsuario.push(plan)
    // Habito.DesdeTareaDePlan: copia la tarea y arranca Activo.
    db.habitos.push(
      ...plantilla.tareas.map((tarea): HabitoMock => ({
        id: crypto.randomUUID(),
        usuarioId,
        nombre: tarea.nombre,
        periodicidad: tarea.periodicidad,
        cantidadObjetivo: tarea.cantidad,
        unidad: tarea.unidad,
        estado: 'Activo',
        fechaInicioCiclo: inicio,
        registros: [],
        planUsuarioId: plan.id,
        descripcion: tarea.instrucciones,
        versionMinima: tarea.versionMinima,
        ancla: tarea.anclaSugerida,
      })),
    )
    return HttpResponse.json(planADto(plan, ahora), {
      status: 201,
      headers: { Location: `/api/my-plans/${plan.id}` },
    })
  }),

  http.get('/api/my-plans', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const ahora = Date.now()
    return HttpResponse.json(planesDe(sesion.usuario.id).map((p) => planADto(p, ahora)))
  }),

  http.get<{ id: string }>('/api/my-plans/:id', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const plan = planPropio(params.id, sesion.usuario.id)
    if (plan instanceof Response) return plan
    return HttpResponse.json(planADto(plan))
  }),

  // Pausar (con motivo), reanudar, completar (solo al llegar al final) o cancelar. Los hábitos acompañan.
  http.post<{ id: string }>('/api/my-plans/:id/transition', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<CambiarEstadoPlanRequest>(request)
    if (
      !body ||
      !esUno(ESTADOS_PLAN_USUARIO, body.nuevoEstado) ||
      !nuloO(body.motivoPausa, (v) => esUno(MOTIVOS_PAUSA, v)) ||
      (body.conservarHabitos !== undefined && typeof body.conservarHabitos !== 'boolean')
    )
      return respuestas.invalida()
    const plan = planPropio(params.id, sesion.usuario.id)
    if (plan instanceof Response) return plan

    const ahora = Date.now()
    const llegoFin = llegoFechaFin(plan, ahora)
    const delPlan = habitosDe(plan)
    const pausaAbierta = plan.pausas.find((x) => x.finUtc === null)
    const nuevo = body.nuevoEstado
    // El motivo se exige antes de mirar el estado del plan (ArgumentException: 400 sin código).
    if (nuevo === 'Pausado' && !body.motivoPausa) return argumentoInvalido()

    // Primero la transición del plan (es la que puede rechazar); los hábitos siguen solo si fue válida.
    const rechazo = transicionRechazada(plan.estado, nuevo, llegoFin)
    if (rechazo) return rechazo

    if (nuevo === 'Pausado') {
      // Un plan que ya llegó a su fin se completa: pausarlo sería una forma de no terminarlo.
      if (llegoFin)
        return respuestas.codigo(
          'transicion_plan_invalida',
          'El plan ya llegó a su final: puedes completarlo o extenderlo.',
        )
      plan.pausas.push({
        inicioUtc: new Date(ahora).toISOString(),
        finUtc: null,
        motivo: body.motivoPausa!,
      })
      plan.motivoPausa = body.motivoPausa!
      pausarHabitos(delPlan)
    } else if (nuevo === 'Activo') {
      // Los días locales de la pausa se agregan al final: la persona no pierde semanas del programa.
      if (pausaAbierta) {
        pausaAbierta.finUtc = new Date(ahora).toISOString()
        const zona = zonaDe(plan.usuarioId)
        const dias = Math.max(
          0,
          diaLocal(zona, ahora) - diaLocal(zona, Date.parse(pausaAbierta.inicioUtc)),
        )
        plan.fechaFinPrevista = new Date(
          Date.parse(plan.fechaFinPrevista) + dias * DIA_MS,
        ).toISOString()
      }
      plan.motivoPausa = null
      // Solo vuelven los que el plan pausó: uno que la persona dejó en pausa a propósito sigue así.
      for (const habito of delPlan.filter((h) => h.estado === 'Pausado' && h.pausadoPorPlan)) {
        habito.estado = 'Activo'
        habito.pausadoPorPlan = false
      }
    } else {
      // Completado o Cancelado. Terminar no borra nada: si la persona conserva los hábitos, siguen
      // activos y vinculados al plan; si no, quedan en pausa.
      if (pausaAbierta) pausaAbierta.finUtc = new Date(ahora).toISOString()
      plan.motivoPausa = null
      if (body.conservarHabitos !== true) pausarHabitos(delPlan)
    }
    plan.estado = nuevo
    return HttpResponse.json(planADto(plan, ahora))
  }),

  // Un ajuste que la persona eligió: modo suave (7 días) o extender una semana (máximo 2 veces).
  http.post<{ id: string }>('/api/my-plans/:id/adjust', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<{ tipo: TipoAjuste }>(request)
    if (!body || !esUno(TIPOS_AJUSTE, body.tipo)) return respuestas.invalida()
    const plan = planPropio(params.id, sesion.usuario.id)
    if (plan instanceof Response) return plan
    // Pausar es una transición (con su motivo): no va por aquí.
    if (body.tipo === 'Pausar') return argumentoInvalido()

    const ahora = Date.now()
    if (plan.estado !== 'Activo')
      return respuestas.codigo('ajuste_no_disponible', 'Solo se puede ajustar un plan activo.')
    if (body.tipo === 'ModoSuave') {
      if (enModoSuave(plan, ahora))
        return respuestas.codigo('ajuste_no_disponible', 'El modo suave ya está activo.')
      plan.modoSuaveHasta = new Date(ahora + DIAS_MODO_SUAVE * DIA_MS).toISOString()
    } else {
      if (plan.extensiones >= MAXIMO_EXTENSIONES)
        return respuestas.codigo(
          'limite_extensiones',
          `Un plan se puede extender ${MAXIMO_EXTENSIONES} veces como máximo.`,
        )
      plan.extensiones++
      plan.fechaFinPrevista = new Date(
        Date.parse(plan.fechaFinPrevista) + DIAS_POR_SEMANA * DIA_MS,
      ).toISOString()
    }
    return HttpResponse.json(planADto(plan, ahora))
  }),

  http.put<{ id: string }>('/api/my-plans/:id/reminder', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<RecordatorioPlanRequest>(request)
    if (
      !body ||
      !nuloO(body.hora, esHora) ||
      (body.recordatoriosActivos !== undefined && typeof body.recordatoriosActivos !== 'boolean')
    )
      return respuestas.invalida()
    const plan = planPropio(params.id, sesion.usuario.id)
    if (plan instanceof Response) return plan

    const activos = body.recordatoriosActivos === true
    // Para activarlos hace falta una hora: la que llega o la que el plan ya tenía.
    if (activos && !body.hora && plan.horaRecordatorio === null) return argumentoInvalido()
    if (body.hora) plan.horaRecordatorio = body.hora.slice(0, 5)
    plan.recordatoriosActivos = activos
    return HttpResponse.json(planADto(plan))
  }),

  // Las tareas de hoy de los planes activos, el mensaje del día y la celebración de la semana.
  http.get('/api/today', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    return HttpResponse.json(hoyDe(sesion.usuario.id))
  }),

  http.get('/api/progress/summary', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    return HttpResponse.json(resumenDe(sesion.usuario.id))
  }),

  // Cada logro se felicita una sola vez.
  http.post('/api/progress/achievements/seen', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const ahora = new Date().toISOString()
    for (const logro of db.logros.filter((l) => l.usuarioId === sesion.usuario.id))
      logro.vistoEn ??= ahora
    return new HttpResponse(null, { status: 204 })
  }),
]
