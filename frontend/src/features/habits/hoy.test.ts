import { describe, expect, it } from 'vitest'
import {
  cumplimientoSemanal,
  rachaDeHoy,
  rachaMasLarga,
  resumenDeHoy,
  saludoSegunHora,
  semanaDelPlan,
  semanaEnCurso,
  tareasDeHoy,
} from './hoy'
import type { HabitoDto } from './types'

// Jueves 5 de noviembre de 2026, 15:00 UTC.
const AHORA = new Date('2026-11-05T15:00:00Z')
const dia = (offset: number) =>
  new Date(Date.UTC(2026, 10, 5 + offset, 9)).toISOString().replace('Z', '')

function habito(
  parcial: Omit<Partial<HabitoDto>, 'registros'> & { registros?: [number, number][] },
): HabitoDto {
  const { registros = [], ...resto } = parcial
  return {
    id: crypto.randomUUID(),
    nombre: 'Leer',
    periodicidad: 'Diaria',
    cantidadObjetivo: 10,
    unidad: 'páginas',
    estado: 'Activo',
    cumpleMetaFinal: false,
    fechaInicioCiclo: dia(-30),
    ...resto,
    registros: registros.map(([offset, cantidad], i) => ({
      id: String(i),
      cantidad,
      fecha: dia(offset),
      unidad: 'páginas',
    })),
  }
}

describe('tareas de hoy', () => {
  it('solo lista hábitos Activos y calcula lo que falta', () => {
    const tareas = tareasDeHoy(
      [
        habito({ nombre: 'Leer', registros: [[0, 4]] }),
        habito({ nombre: 'Meditar', estado: 'Pausado' }),
        habito({ nombre: 'Agua', cantidadObjetivo: 8, registros: [[0, 8]] }),
      ],
      AHORA,
    )
    expect(tareas.map((t) => t.habito.nombre)).toEqual(['Leer', 'Agua'])
    expect(tareas[0]).toMatchObject({ avance: 4, restante: 6, hecha: false })
    expect(tareas[1]).toMatchObject({ restante: 0, hecha: true })
    expect(resumenDeHoy(tareas)).toEqual({ hechas: 1, total: 2, porcentaje: 50 })
  })

  it('un semanal cuenta lo de la semana ISO en curso', () => {
    const correr = habito({
      periodicidad: 'Semanal',
      cantidadObjetivo: 15,
      // Lunes 2 (−3) y miércoles 4 (−1) de esta semana; el domingo 1 (−4) es la anterior.
      registros: [
        [-4, 20],
        [-3, 5],
        [-1, 6],
      ],
    })
    expect(tareasDeHoy([correr], AHORA)[0]).toMatchObject({ avance: 11, restante: 4 })
  })

  it('sin tareas el resumen es 0 de 0, nunca NaN', () => {
    expect(resumenDeHoy([])).toEqual({ hechas: 0, total: 0, porcentaje: 0 })
  })
})

describe('rachas', () => {
  const leer = habito({
    registros: [
      [-9, 10],
      [-8, 10],
      [-7, 10],
      [-6, 10],
      [-4, 10],
      [-3, 12],
      [-2, 10],
      [-1, 10],
    ],
  })

  it('la racha de hoy no se rompe mientras el día siga abierto', () => {
    expect(rachaDeHoy([leer], AHORA)).toBe(4)
  })

  it('el récord es la serie más larga de días cumplidos', () => {
    expect(rachaMasLarga([leer])).toBe(4)
    const otro = habito({
      registros: [
        [-20, 10],
        [-19, 10],
        [-18, 10],
        [-17, 10],
        [-16, 10],
      ],
    })
    expect(rachaMasLarga([leer, otro])).toBe(5)
  })

  it('sin hábitos diarios activos la racha es 0', () => {
    expect(rachaDeHoy([habito({ estado: 'Pausado' })], AHORA)).toBe(0)
  })
})

describe('cumplimiento semanal y tira de la semana', () => {
  it('cuenta los últimos 7 días desde el inicio del ciclo', () => {
    // Ciclo empezado hace 3 días: se evalúan 4 días (−3..0) y se cumplen 2.
    const nuevo = habito({
      fechaInicioCiclo: dia(-3),
      registros: [
        [-3, 10],
        [-1, 10],
        [-10, 10],
      ],
    })
    expect(cumplimientoSemanal([nuevo], AHORA)).toBe(50)
    expect(cumplimientoSemanal([], AHORA)).toBeNull()
  })

  it('marca cada día de lunes a domingo', () => {
    const a = habito({
      registros: [
        [-3, 10],
        [-2, 10],
      ],
    })
    const b = habito({ registros: [[-3, 10]] })
    const semana = semanaEnCurso([a, b], AHORA)
    expect(semana.map((d) => d.estado)).toEqual([
      'completo', // lun 2: los dos
      'parcial', // mar 3: solo a
      'vacio',
      'vacio', // jue 5: hoy
      'futuro',
      'futuro',
      'futuro',
    ])
    expect(semana[3].hoy).toBe(true)
    expect(semana[0].fecha.toISOString()).toBe('2026-11-02T00:00:00.000Z')
  })
})

describe('semana del plan', () => {
  it('empieza en 1 y no pasa de la duración', () => {
    expect(semanaDelPlan('2026-11-05T00:00:00Z', 8, AHORA)).toBe(1)
    expect(semanaDelPlan('2026-10-19T00:00:00Z', 8, AHORA)).toBe(3)
    expect(semanaDelPlan('2026-01-01T00:00:00Z', 8, AHORA)).toBe(8)
  })
})

describe('bordes de las métricas de hoy', () => {
  it('lo que falta nunca arrastra ruido de decimales ni baja de 0,01', () => {
    const [a] = tareasDeHoy([habito({ cantidadObjetivo: 0.3, registros: [[0, 0.1]] })], AHORA)
    expect(a.restante).toBe(0.2)
    const [b] = tareasDeHoy([habito({ cantidadObjetivo: 10, registros: [[0, 9.995]] })], AHORA)
    expect(b.restante).toBe(0.01)
    expect(b.hecha).toBe(false)
    const [c] = tareasDeHoy([habito({ cantidadObjetivo: 0.3, registros: [[0, 0.3]] })], AHORA)
    expect(c).toMatchObject({ hecha: true, restante: 0 })
  })

  it('la tira solo cuenta un hábito desde el día en que empezó su ciclo', () => {
    // A existe desde antes; B nació el miércoles 4. El lunes solo aplica A, que cumplió.
    const a = habito({ registros: [[-3, 10]] })
    const b = habito({ fechaInicioCiclo: dia(-1) })
    const semana = semanaEnCurso([a, b], AHORA)
    expect(semana[0].estado).toBe('completo')
    expect(semana[2].estado).toBe('vacio') // mié 4: ni A ni B cumplieron
    const conB = semanaEnCurso(
      [a, habito({ fechaInicioCiclo: dia(-1), registros: [[-1, 10]] })],
      AHORA,
    )
    expect(conB[2].estado).toBe('parcial') // mié 4: B cumplió, A no
    // Si todos nacieron hoy, los días anteriores no aplican.
    const nuevo = habito({ fechaInicioCiclo: dia(0) })
    expect(
      semanaEnCurso([nuevo], AHORA)
        .slice(0, 3)
        .map((d) => d.estado),
    ).toEqual(['vacio', 'vacio', 'vacio'])
  })

  it('la semana ISO es correcta en lunes y en domingo, y con listas vacías', () => {
    const lunes = new Date('2026-11-02T00:00:00Z')
    const domingo = new Date('2026-11-08T23:59:59Z')
    expect(semanaEnCurso([], lunes).map((d) => d.hoy)).toEqual([
      true,
      false,
      false,
      false,
      false,
      false,
      false,
    ])
    expect(semanaEnCurso([], domingo).map((d) => d.hoy)).toEqual([
      false,
      false,
      false,
      false,
      false,
      false,
      true,
    ])
    expect(semanaEnCurso([], domingo)[0].fecha.toISOString()).toBe('2026-11-02T00:00:00.000Z')
    expect(rachaMasLarga([])).toBe(0)
    expect(rachaDeHoy([], AHORA)).toBe(0)
  })

  it('el cambio de día UTC separa los avances de hoy y de mañana', () => {
    const h = habito({ registros: [[0, 10]] })
    const finDelDia = new Date('2026-11-05T23:59:59.999Z')
    const inicioSiguiente = new Date('2026-11-06T00:00:00.000Z')
    expect(tareasDeHoy([h], finDelDia)[0].hecha).toBe(true)
    expect(tareasDeHoy([h], inicioSiguiente)[0].hecha).toBe(false)
  })

  it('el récord conserva el historial anterior al ciclo (reconfigurar no lo borra)', () => {
    const h = habito({
      fechaInicioCiclo: dia(-2),
      registros: [
        [-5, 10],
        [-4, 10],
        [-3, 10],
        [-1, 10],
      ],
    })
    // Los días −5, −4 y −3 son de antes del ciclo, pero siguen contando como serie de 3.
    expect(rachaMasLarga([h])).toBe(3)
  })

  it('un plan que empieza en el futuro es la semana 1', () => {
    expect(semanaDelPlan('2026-12-01T00:00:00Z', 8, AHORA)).toBe(1)
  })

  it('el saludo cambia a las 12 y a las 19 horas', () => {
    expect([11, 12, 18, 19].map(saludoSegunHora)).toEqual([
      'Buenos días',
      'Buenas tardes',
      'Buenas tardes',
      'Buenas noches',
    ])
  })
})
