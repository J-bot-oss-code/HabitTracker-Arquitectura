import { parsearFechaUtc } from '@/shared/lib/fechas'
import { rachaMasLarga, sumasPorDia } from './hoy'
import type { HabitoDto } from './types'

// Métricas de la pantalla Progreso (especificación §5.4 y §6.4). Como las de "Hoy", son de
// presentación: se derivan de los avances que ya devuelve la API y no deciden nada de negocio.
// Los días son días UTC (RD-11).

const DIA_MS = 86_400_000
const indiceDia = (t: number) => Math.floor(t / DIA_MS)

export type NivelConstancia = 0 | 1 | 2 | 3

export interface DiaConstancia {
  // Inicio del día UTC.
  fecha: Date
  cumplidos: number
  // Hábitos diarios que aplicaban ese día (ya habían empezado su ciclo y no eran Planificado).
  total: number
  nivel: NivelConstancia
  hoy: boolean
}

// 0 nada, 1 menos de la mitad, 2 la mitad o más, 3 todo lo que aplicaba.
function nivelDe(cumplidos: number, total: number): NivelConstancia {
  if (total === 0 || cumplidos === 0) return 0
  if (cumplidos === total) return 3
  return cumplidos / total >= 0.5 ? 2 : 1
}

// Constancia de los últimos `semanas` completas, de lunes a domingo. La rejilla termina en el
// domingo de la semana en curso; los días que aún no llegan salen como nivel 0 sin aplicar.
export function constanciaPorDia(
  habitos: HabitoDto[],
  semanas = 10,
  ahora = new Date(),
): DiaConstancia[] {
  const hoy = indiceDia(ahora.getTime())
  const lunesActual = hoy - ((hoy + 3) % 7)
  const primero = lunesActual - (semanas - 1) * 7
  const diarios = habitos
    .filter((h) => h.periodicidad === 'Diaria' && h.estado !== 'Planificado')
    .map((h) => ({
      h,
      sumas: sumasPorDia(h),
      inicio: indiceDia(parsearFechaUtc(h.fechaInicioCiclo).getTime()),
    }))

  return Array.from({ length: semanas * 7 }, (_, i) => {
    const dia = primero + i
    const fecha = new Date(dia * DIA_MS)
    if (dia > hoy) return { fecha, cumplidos: 0, total: 0, nivel: 0, hoy: false }
    // Un hábito Activo cuenta desde el día en que empezó su ciclo. Uno Completado o Pausado ya
    // no se exige: solo suma (como cumplido) los días en que sí cumplió, para que su historial
    // cuente y nunca penalice los días de lo que sigue en marcha.
    const cumple = ({ h, sumas }: (typeof diarios)[number]) =>
      (sumas.get(dia) ?? 0) >= h.cantidadObjetivo
    const aplican = diarios.filter((d) => (d.h.estado === 'Activo' ? d.inicio <= dia : cumple(d)))
    const cumplidos = aplican.filter(cumple).length
    return {
      fecha,
      cumplidos,
      total: aplican.length,
      nivel: nivelDe(cumplidos, aplican.length),
      hoy: dia === hoy,
    }
  })
}

export interface Records {
  rachaMasLarga: number
  avancesRegistrados: number
  habitosCompletados: number
}

// Los avances son los registros que devuelve la API para cada hábito (historial completo).
export function recordsDe(habitos: HabitoDto[]): Records {
  return {
    rachaMasLarga: rachaMasLarga(habitos),
    avancesRegistrados: habitos.reduce((total, h) => total + h.registros.length, 0),
    habitosCompletados: habitos.filter((h) => h.estado === 'Completado').length,
  }
}

export interface Logro {
  clave: string
  titulo: string
  descripcion: string
  obtenido: boolean
  // Avance hacia el logro, para decir cuánto falta ("3 de 7").
  actual: number
  meta: number
}

// Insignias por constancia, no por peso (docs/mejoras §4.6): ninguna premia bajar de peso.
export function logrosDe(records: Records): Logro[] {
  return [
    {
      clave: 'primer-avance',
      titulo: 'Primer avance',
      descripcion: 'Registraste tu primer avance.',
      obtenido: records.avancesRegistrados >= 1,
      actual: Math.min(records.avancesRegistrados, 1),
      meta: 1,
    },
    {
      clave: 'primera-semana',
      titulo: 'Primera semana completa',
      descripcion: 'Siete días seguidos cumpliendo un hábito diario.',
      obtenido: records.rachaMasLarga >= 7,
      actual: Math.min(records.rachaMasLarga, 7),
      meta: 7,
    },
    {
      clave: 'veinticinco-avances',
      titulo: '25 avances',
      descripcion: 'Registraste 25 avances en total.',
      obtenido: records.avancesRegistrados >= 25,
      actual: Math.min(records.avancesRegistrados, 25),
      meta: 25,
    },
    {
      clave: 'constancia-30',
      titulo: '30 días de racha',
      descripcion: 'Treinta días seguidos cumpliendo un hábito diario.',
      obtenido: records.rachaMasLarga >= 30,
      actual: Math.min(records.rachaMasLarga, 30),
      meta: 30,
    },
    {
      clave: 'meta-cumplida',
      titulo: 'Meta cumplida',
      descripcion: 'Completaste un hábito.',
      obtenido: records.habitosCompletados >= 1,
      actual: Math.min(records.habitosCompletados, 1),
      meta: 1,
    },
  ]
}
