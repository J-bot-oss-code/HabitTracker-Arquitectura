import { describe, expect, it } from 'vitest'
import { constanciaPorDia } from './progreso'
import type { HabitoDto } from './types'

// Jueves 5 de noviembre de 2026, 15:00 UTC.
const AHORA = new Date('2026-11-05T15:00:00Z')
const dia = (offset: number) =>
  new Date(Date.UTC(2026, 10, 5 + offset, 9)).toISOString().replace('Z', '')
const inicioDia = (offset: number) => Date.UTC(2026, 10, 5 + offset)

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
    fechaInicioCiclo: dia(-100),
    ...resto,
    registros: registros.map(([offset, cantidad], i) => ({
      id: String(i),
      cantidad,
      fecha: dia(offset),
      unidad: 'páginas',
    })),
  }
}

describe('constancia por día', () => {
  it('cubre semanas completas de lunes a domingo, terminando en la semana actual', () => {
    const dias = constanciaPorDia([], 10, AHORA)
    expect(dias).toHaveLength(70)
    expect(dias[0].fecha.toISOString()).toBe('2026-08-31T00:00:00.000Z') // un lunes
    expect(dias[69].fecha.toISOString()).toBe('2026-11-08T00:00:00.000Z') // domingo de esta semana
    expect(dias.filter((d) => d.hoy)).toHaveLength(1)
    expect(dias[66].hoy).toBe(true) // jueves 5
  })

  it('asigna nivel según la proporción de hábitos cumplidos ese día', () => {
    const a = habito({
      registros: [
        [-1, 10],
        [-2, 10],
        [-3, 10],
      ],
    })
    const b = habito({
      registros: [
        [-1, 10],
        [-2, 10],
      ],
    })
    const c = habito({ registros: [[-1, 10]] })
    const dias = constanciaPorDia([a, b, c], 10, AHORA)
    const de = (offset: number) => dias.find((d) => d.fecha.getTime() === inicioDia(offset))
    expect(de(-1)).toMatchObject({ cumplidos: 3, total: 3, nivel: 3 })
    expect(de(-2)).toMatchObject({ cumplidos: 2, total: 3, nivel: 2 })
    expect(de(-3)).toMatchObject({ cumplidos: 1, total: 3, nivel: 1 })
    expect(de(-4)).toMatchObject({ cumplidos: 0, nivel: 0 })
  })

  it('los días futuros y los anteriores al ciclo no aplican', () => {
    const nuevo = habito({ fechaInicioCiclo: dia(-1), registros: [[-1, 10]] })
    const dias = constanciaPorDia([nuevo], 2, AHORA)
    const de = (offset: number) => dias.find((d) => d.fecha.getTime() === inicioDia(offset))
    expect(de(-2)).toMatchObject({ total: 0, nivel: 0 }) // antes del ciclo
    expect(de(-1)).toMatchObject({ total: 1, nivel: 3 })
    expect(de(2)).toMatchObject({ total: 0, nivel: 0, hoy: false }) // futuro
  })

  it('ignora los hábitos Planificados y los semanales', () => {
    const planificado = habito({ estado: 'Planificado', registros: [[-1, 10]] })
    const semanal = habito({ periodicidad: 'Semanal', registros: [[-1, 10]] })
    const dias = constanciaPorDia([planificado, semanal], 2, AHORA)
    expect(dias.every((d) => d.total === 0)).toBe(true)
  })
})

describe('constancia con hábitos que ya no están activos', () => {
  it('un hábito Completado no penaliza los días en que no cumplió', () => {
    const activo = habito({ registros: [[-1, 10]] })
    const completado = habito({ estado: 'Completado', registros: [[-3, 10]] })
    const dias = constanciaPorDia([activo, completado], 10, AHORA)
    const de = (offset: number) => dias.find((d) => d.fecha.getTime() === inicioDia(offset))
    // Ayer cumplió todo lo que seguía en marcha: "Todo hecho", no "parcial".
    expect(de(-1)).toMatchObject({ cumplidos: 1, total: 1, nivel: 3 })
    // Hace 3 días solo cumplió el completado, y el activo (que sí aplicaba) no.
    expect(de(-3)).toMatchObject({ cumplidos: 1, total: 2, nivel: 2 })
  })

  it('un hábito Pausado conserva su historial sin exigirse', () => {
    const pausado = habito({ estado: 'Pausado', registros: [[-2, 10]] })
    const dias = constanciaPorDia([pausado], 10, AHORA)
    const de = (offset: number) => dias.find((d) => d.fecha.getTime() === inicioDia(offset))
    expect(de(-2)).toMatchObject({ cumplidos: 1, total: 1, nivel: 3 })
    expect(de(-1)).toMatchObject({ total: 0, nivel: 0 })
  })

  it('el cambio de día UTC mueve el "hoy" de la rejilla', () => {
    const finDelDia = new Date('2026-11-05T23:59:59.999Z')
    const inicioSiguiente = new Date('2026-11-06T00:00:00.000Z')
    expect(constanciaPorDia([], 10, finDelDia).findIndex((d) => d.hoy)).toBe(66) // jueves
    expect(constanciaPorDia([], 10, inicioSiguiente).findIndex((d) => d.hoy)).toBe(67) // viernes
  })
})
