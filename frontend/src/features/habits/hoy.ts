import { parsearFechaUtc } from '@/shared/lib/fechas'
import { avanceDelPeriodo, rachaActual } from './estados'
import type { HabitoDto } from './types'

// Métricas del panel "Hoy" (especificación §5.3). Son de presentación: el servidor sigue
// decidiendo las transiciones y la meta final. Todas se calculan por día UTC (RD-11), igual
// que los avances.

const DIA_MS = 86_400_000
const EPSILON = 1e-9
const indiceDia = (t: number) => Math.floor(t / DIA_MS)
// El 1-ene-1970 fue jueves: con +3 las semanas empiezan en lunes (ISO).
const diaDeLaSemana = (dia: number) => (dia + 3) % 7

export interface TareaDeHoy {
  habito: HabitoDto
  avance: number
  objetivo: number
  // Lo que falta para cumplir el período; 0 si ya está hecha.
  restante: number
  hecha: boolean
}

// Las tareas de hoy son los hábitos Activos: los diarios cuentan lo de hoy y los semanales lo
// de la semana en curso.
export function tareasDeHoy(habitos: HabitoDto[], ahora = new Date()): TareaDeHoy[] {
  return habitos
    .filter((h) => h.estado === 'Activo')
    .map((habito) => {
      const avance = avanceDelPeriodo(habito, ahora)
      const faltante = habito.cantidadObjetivo - avance
      const hecha = faltante <= EPSILON
      // Lo que se registra al marcar: nunca menos de 0,01 (mínimo del servidor) y redondeado
      // hacia arriba a 2 decimales sin arrastrar el ruido de la resta (0,3 - 0,1).
      const restante = hecha ? 0 : Math.max(0.01, Math.ceil(faltante * 100 - EPSILON) / 100)
      return { habito, avance, objetivo: habito.cantidadObjetivo, restante, hecha }
    })
}

export function resumenDeHoy(tareas: TareaDeHoy[]) {
  const hechas = tareas.filter((t) => t.hecha).length
  const total = tareas.length
  return { hechas, total, porcentaje: total === 0 ? 0 : (hechas / total) * 100 }
}

// Sumas por día UTC dentro del ciclo actual de un hábito.
export function sumasPorDia(habito: HabitoDto): Map<number, number> {
  const inicio = parsearFechaUtc(habito.fechaInicioCiclo).getTime()
  const sumas = new Map<number, number>()
  for (const r of habito.registros) {
    const t = parsearFechaUtc(r.fecha).getTime()
    if (t < inicio) continue
    sumas.set(indiceDia(t), (sumas.get(indiceDia(t)) ?? 0) + r.cantidad)
  }
  return sumas
}

const diariosActivos = (habitos: HabitoDto[]) =>
  habitos.filter((h) => h.estado === 'Activo' && h.periodicidad === 'Diaria')

// Racha que se muestra en el panel: la mayor de los hábitos diarios activos, en días.
export function rachaDeHoy(habitos: HabitoDto[], ahora = new Date()): number {
  return Math.max(0, ...diariosActivos(habitos).map((h) => rachaActual(h, ahora)))
}

// Sumas por día UTC de TODO el historial del hábito (reconfigurar inicia un ciclo nuevo pero
// conserva los avances). Sirve para los récords, que no deben perderse al reconfigurar.
function sumasHistoricasPorDia(habito: HabitoDto): Map<number, number> {
  const sumas = new Map<number, number>()
  for (const r of habito.registros) {
    const dia = indiceDia(parsearFechaUtc(r.fecha).getTime())
    sumas.set(dia, (sumas.get(dia) ?? 0) + r.cantidad)
  }
  return sumas
}

// Récord: la serie más larga de días seguidos que cumplieron el objetivo vigente, en cualquier
// hábito diario (activo o no), con todo su historial.
export function rachaMasLarga(habitos: HabitoDto[]): number {
  let record = 0
  for (const h of habitos.filter((x) => x.periodicidad === 'Diaria')) {
    const sumas = sumasHistoricasPorDia(h)
    const cumplidos = [...sumas.entries()]
      .filter(([, suma]) => suma >= h.cantidadObjetivo)
      .map(([dia]) => dia)
      .sort((a, b) => a - b)
    let serie = 0
    cumplidos.forEach((dia, i) => {
      serie = i > 0 && cumplidos[i - 1] === dia - 1 ? serie + 1 : 1
      record = Math.max(record, serie)
    })
  }
  return record
}

// Porcentaje de días cumplidos en los últimos 7 días (incluido hoy) por los hábitos diarios
// activos. Solo cuentan los días desde que empezó el ciclo de cada hábito; null si no hay
// ningún día que evaluar.
export function cumplimientoSemanal(habitos: HabitoDto[], ahora = new Date()): number | null {
  const hoy = indiceDia(ahora.getTime())
  let evaluados = 0
  let cumplidos = 0
  for (const h of diariosActivos(habitos)) {
    const sumas = sumasPorDia(h)
    const inicio = indiceDia(parsearFechaUtc(h.fechaInicioCiclo).getTime())
    for (let dia = Math.max(hoy - 6, inicio); dia <= hoy; dia++) {
      evaluados += 1
      if ((sumas.get(dia) ?? 0) >= h.cantidadObjetivo) cumplidos += 1
    }
  }
  return evaluados === 0 ? null : (cumplidos / evaluados) * 100
}

export type EstadoDia = 'completo' | 'parcial' | 'vacio' | 'futuro'

export interface DiaDeLaSemana {
  // Instante UTC del inicio del día, para la etiqueta y la clave.
  fecha: Date
  estado: EstadoDia
  hoy: boolean
}

// Tira "Esta semana" (WeekStrip, §3): lunes a domingo de la semana ISO en curso. Completo si
// todos los hábitos diarios activos cumplieron, parcial si alguno, futuro si aún no llega.
export function semanaEnCurso(habitos: HabitoDto[], ahora = new Date()): DiaDeLaSemana[] {
  const hoy = indiceDia(ahora.getTime())
  const lunes = hoy - diaDeLaSemana(hoy)
  const diarios = diariosActivos(habitos).map((h) => ({
    h,
    sumas: sumasPorDia(h),
    inicio: indiceDia(parsearFechaUtc(h.fechaInicioCiclo).getTime()),
  }))
  return Array.from({ length: 7 }, (_, i) => {
    const dia = lunes + i
    const fecha = new Date(dia * DIA_MS)
    if (dia > hoy) return { fecha, estado: 'futuro', hoy: false }
    // Un hábito solo cuenta desde el día en que empezó su ciclo (igual que cumplimientoSemanal).
    const aplican = diarios.filter(({ inicio }) => inicio <= dia)
    const cumplen = aplican.filter(({ h, sumas }) => (sumas.get(dia) ?? 0) >= h.cantidadObjetivo)
    const estado: EstadoDia =
      aplican.length > 0 && cumplen.length === aplican.length
        ? 'completo'
        : cumplen.length > 0
          ? 'parcial'
          : 'vacio'
    return { fecha, estado, hoy: dia === hoy }
  })
}

// Semana del plan según la meta del perfil: 1 la primera semana; tope en la duración.
export function semanaDelPlan(
  inicioPlan: string,
  semanasTotales: number,
  ahora = new Date(),
): number {
  const dias = indiceDia(ahora.getTime()) - indiceDia(parsearFechaUtc(inicioPlan).getTime())
  return Math.min(semanasTotales, Math.max(1, Math.floor(dias / 7) + 1))
}

// Saludo según la hora local de quien mira la pantalla (no se guarda ni se envía).
export function saludoSegunHora(hora: number): string {
  if (hora < 12) return 'Buenos días'
  if (hora < 19) return 'Buenas tardes'
  return 'Buenas noches'
}
