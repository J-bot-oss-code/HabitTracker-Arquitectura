import { describe, expect, it } from 'vitest'
import {
  accionesDisponibles,
  avanceDelPeriodo,
  avancePorDia,
  rachaActual,
  totalDelCiclo,
} from './estados'
import type { HabitoDto } from './types'

const base: HabitoDto = {
  id: 'h-1',
  nombre: 'Leer',
  periodicidad: 'Diaria',
  cantidadObjetivo: 20,
  unidad: 'páginas',
  estado: 'Planificado',
  cumpleMetaFinal: false,
  fechaInicioCiclo: '2026-10-01T00:00:00',
  registros: [],
}

const destinos = (h: HabitoDto) => accionesDisponibles(h).map((a) => a.destino)

describe('accionesDisponibles (espejo de TransicionesHabito)', () => {
  it('Planificado solo puede activarse', () => {
    expect(destinos(base)).toEqual(['Activo'])
  })

  it('Activo puede pausarse o completarse', () => {
    expect(destinos({ ...base, estado: 'Activo' })).toEqual(['Pausado', 'Completado'])
  })

  it('Completar queda deshabilitado mientras no se cumpla la meta', () => {
    const completar = accionesDisponibles({ ...base, estado: 'Activo' }).find(
      (a) => a.destino === 'Completado',
    )
    expect(completar?.habilitada).toBe(false)
    const conMeta = accionesDisponibles({ ...base, estado: 'Activo', cumpleMetaFinal: true })
    expect(conMeta.find((a) => a.destino === 'Completado')?.habilitada).toBe(true)
  })

  it('Pausado solo puede reanudarse (Pausado → Completado está prohibido)', () => {
    expect(destinos({ ...base, estado: 'Pausado' })).toEqual(['Activo'])
  })

  it('Completado es terminal', () => {
    expect(destinos({ ...base, estado: 'Completado' })).toEqual([])
  })
})

describe('avanceDelPeriodo', () => {
  const ahora = new Date('2026-10-29T15:00:00Z') // jueves

  it('suma solo los avances de hoy en un hábito diario', () => {
    const habito: HabitoDto = {
      ...base,
      registros: [
        { id: 'r1', cantidad: 5, fecha: '2026-10-29T00:00:00', unidad: 'páginas' },
        { id: 'r2', cantidad: 7, fecha: '2026-10-28T00:00:00', unidad: 'páginas' },
      ],
    }
    expect(avanceDelPeriodo(habito, ahora)).toBe(5)
  })

  it('suma la semana ISO (desde el lunes) en un hábito semanal', () => {
    const habito: HabitoDto = {
      ...base,
      periodicidad: 'Semanal',
      registros: [
        { id: 'r1', cantidad: 3, fecha: '2026-10-26T00:00:00Z', unidad: 'km' }, // lunes
        { id: 'r2', cantidad: 4, fecha: '2026-10-29T00:00:00Z', unidad: 'km' },
        { id: 'r3', cantidad: 9, fecha: '2026-10-25T00:00:00Z', unidad: 'km' }, // domingo anterior
      ],
    }
    expect(avanceDelPeriodo(habito, ahora)).toBe(7)
  })

  it('ignora avances anteriores al inicio del ciclo actual', () => {
    const habito: HabitoDto = {
      ...base,
      fechaInicioCiclo: '2026-10-29T12:00:00Z',
      registros: [{ id: 'r1', cantidad: 5, fecha: '2026-10-29T00:00:00Z', unidad: 'páginas' }],
    }
    expect(avanceDelPeriodo(habito, ahora)).toBe(0)
  })
})

describe('rachaActual', () => {
  const ahora = new Date('2026-10-29T15:00:00Z')
  const dia = (d: number, cantidad: number) => ({
    id: `r${d}`,
    cantidad,
    fecha: `2026-10-${String(d).padStart(2, '0')}T00:00:00Z`,
    unidad: 'páginas',
  })
  const habito = (
    registros: HabitoDto['registros'],
    extra: Partial<HabitoDto> = {},
  ): HabitoDto => ({
    ...base,
    estado: 'Activo',
    fechaInicioCiclo: '2026-10-01T00:00:00Z',
    registros,
    ...extra,
  })

  it('cuenta los días seguidos que alcanzaron el objetivo', () => {
    expect(rachaActual(habito([dia(28, 20), dia(27, 25), dia(26, 20)]), ahora)).toBe(3)
  })

  it('el día en curso incompleto no rompe la racha', () => {
    expect(rachaActual(habito([dia(29, 5), dia(28, 20), dia(27, 20)]), ahora)).toBe(2)
  })

  it('suma el día en curso cuando ya cumplió', () => {
    expect(rachaActual(habito([dia(29, 20), dia(28, 20)]), ahora)).toBe(2)
  })

  it('un día sin cumplir corta la racha', () => {
    expect(rachaActual(habito([dia(28, 20), dia(27, 3), dia(26, 20)]), ahora)).toBe(1)
  })

  it('sin avances no hay racha', () => {
    expect(rachaActual(habito([]), ahora)).toBe(0)
  })

  it('en hábitos semanales cuenta semanas ISO consecutivas', () => {
    const semanal = habito(
      [dia(27, 15), dia(20, 10), dia(21, 6), dia(12, 4)], // sem. 26-oct: 15 · sem. 19-oct: 16 · sem. 12-oct: 4
      { periodicidad: 'Semanal', cantidadObjetivo: 15 },
    )
    expect(rachaActual(semanal, ahora)).toBe(2)
  })

  it('ignora avances anteriores al ciclo actual', () => {
    const reconfigurado = habito([dia(28, 20), dia(27, 20)], {
      fechaInicioCiclo: '2026-10-28T12:00:00Z',
    })
    expect(rachaActual(reconfigurado, ahora)).toBe(0)
  })
})

describe('avancePorDia y totalDelCiclo', () => {
  const ahora = new Date('2026-10-29T15:00:00Z')
  const h: HabitoDto = {
    ...base,
    estado: 'Activo',
    fechaInicioCiclo: '2026-10-01T00:00:00Z',
    registros: [
      { id: 'a', cantidad: 5, fecha: '2026-10-29T08:00:00Z', unidad: 'páginas' },
      { id: 'b', cantidad: 3, fecha: '2026-10-29T20:00:00Z', unidad: 'páginas' },
      { id: 'c', cantidad: 7, fecha: '2026-10-27T00:00:00Z', unidad: 'páginas' },
    ],
  }

  it('devuelve los últimos días con hoy al final y suma por día', () => {
    const dias = avancePorDia(h, 7, ahora)
    expect(dias).toHaveLength(7)
    expect(dias[6].valor).toBe(8)
    expect(dias[4].valor).toBe(7)
    expect(dias[5].valor).toBe(0)
  })

  it('totaliza el ciclo', () => {
    expect(totalDelCiclo(h)).toBe(15)
  })
})
