import { parsearFechaUtc } from '@/shared/lib/fechas'
import { sumasPorDia } from './hoy'
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
