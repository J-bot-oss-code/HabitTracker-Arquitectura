import { describe, expect, it } from 'vitest'
import { accionesDisponibles, avanceDelPeriodo } from './estados'
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
