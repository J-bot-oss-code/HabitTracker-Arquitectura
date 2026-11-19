import { describe, expect, it } from 'vitest'
import {
  filasDeMedidas,
  lineaDelPlan,
  pesosDelPeriodo,
  puntosReales,
  resumenDePeso,
  textoDuracion,
} from './metricas'
import type { MedicionDto, PesoDto } from './types'

const AHORA = new Date('2026-11-05T15:00:00Z')
const DIA = 86_400_000
const peso = (offset: number, pesoKg: number): PesoDto => ({
  id: String(offset),
  fecha: new Date(AHORA.getTime() + offset * DIA).toISOString(),
  pesoKg,
})
const registros = [peso(-70, 71), peso(-35, 70), peso(-20, 69.5), peso(-7, 69), peso(0, 68.4)]

describe('periodo del peso', () => {
  it('filtra por 4 semanas, 3 meses o todo, de más antiguo a más reciente', () => {
    expect(pesosDelPeriodo(registros, '4semanas', AHORA).map((r) => r.pesoKg)).toEqual([
      69.5, 69, 68.4,
    ])
    expect(pesosDelPeriodo(registros, '3meses', AHORA)).toHaveLength(5)
    const desordenados = [registros[3], registros[0], registros[4]]
    expect(pesosDelPeriodo(desordenados, 'todo', AHORA).map((r) => r.pesoKg)).toEqual([
      71, 69, 68.4,
    ])
    expect(pesosDelPeriodo([], 'todo', AHORA)).toEqual([])
  })
})

describe('resumen y línea del plan', () => {
  it('resume el cambio sin ruido de decimales', () => {
    expect(resumenDePeso(registros)).toEqual({
      inicial: 71,
      actual: 68.4,
      cambio: -2.6,
      dias: 70,
      semanas: 10,
    })
    expect(resumenDePeso([peso(0, 70)])).toMatchObject({ cambio: 0, semanas: 0 })
    expect(resumenDePeso([])).toBeNull()
  })

  it('dibuja el plan desde el primer peso visible hasta el objetivo al final del plan', () => {
    const reales = puntosReales(registros)
    const fin = AHORA.getTime() + 30 * DIA
    expect(lineaDelPlan(reales, 66, fin)).toEqual([reales[0], { t: fin, v: 66 }])
  })

  it('sin objetivo, sin plan o con el plan vencido no hay línea', () => {
    const reales = puntosReales(registros)
    const fin = AHORA.getTime() + 30 * DIA
    expect(lineaDelPlan(reales, null, fin)).toBeUndefined()
    expect(lineaDelPlan(reales, 66, null)).toBeUndefined()
    expect(lineaDelPlan(reales, 66, reales[0].t - DIA)).toBeUndefined()
    expect(lineaDelPlan([], 66, fin)).toBeUndefined()
  })
})

describe('medidas', () => {
  const m = (offset: number, zona: MedicionDto['zona'], valor: number): MedicionDto => ({
    id: `${offset}${zona}`,
    fecha: new Date(AHORA.getTime() + offset * DIA).toISOString(),
    zona,
    valor,
  })

  it('toma la primera y la última medición de cada zona, en el orden de la tabla', () => {
    const filas = filasDeMedidas([
      m(0, 'Cintura', 78),
      m(-60, 'Cintura', 82),
      m(-30, 'Grasa', 27),
      m(-1, 'Grasa', 25.2),
      m(-60, 'Pecho', 96),
    ])
    expect(filas).toEqual([
      { zona: 'Cintura', inicio: 82, hoy: 78, cambio: -4 },
      { zona: 'Pecho', inicio: 96, hoy: 96, cambio: 0 },
      { zona: 'Grasa', inicio: 27, hoy: 25.2, cambio: -1.8 },
    ])
    expect(filasDeMedidas([])).toEqual([])
  })
})

describe('bordes del periodo y del plan', () => {
  it('el día N exacto sigue dentro del periodo aunque sea de madrugada', () => {
    const exacto: PesoDto = {
      id: 'b',
      fecha: new Date(Date.UTC(2026, 9, 8, 0, 0, 0)).toISOString(), // hace 28 días, 00:00Z
      pesoKg: 70,
    }
    expect(pesosDelPeriodo([exacto], '4semanas', AHORA)).toHaveLength(1)
    const antes: PesoDto = { ...exacto, id: 'a', fecha: '2026-10-07T23:59:59Z' }
    expect(pesosDelPeriodo([antes], '4semanas', AHORA)).toHaveLength(0)
  })

  it('una fecha de fin de plan inválida no produce línea', () => {
    expect(lineaDelPlan(puntosReales(registros), 66, Number.NaN)).toBeUndefined()
  })

  it('con menos de una semana la duración se dice en días', () => {
    expect(textoDuracion(3, 0)).toBe('3 días')
    expect(textoDuracion(1, 0)).toBe('1 día')
    expect(textoDuracion(7, 1)).toBe('1 semana')
    expect(textoDuracion(70, 10)).toBe('10 semanas')
    expect(resumenDePeso([peso(-3, 70), peso(0, 69.5)])).toMatchObject({ dias: 3, cambio: -0.5 })
  })
})

describe('plan anclado al primer peso', () => {
  const reales = puntosReales(registros)
  const fin = AHORA.getTime() + 30 * DIA

  it('recortar el periodo no cambia la recta del plan', () => {
    const completa = lineaDelPlan(reales, 66, fin) ?? []
    const desde = reales[3].t
    const recortada = lineaDelPlan(reales, 66, fin, desde) ?? []
    expect(recortada[1]).toEqual(completa[1])
    // El punto inicial recortado está sobre la misma recta.
    const pendiente = (66 - reales[0].v) / (fin - reales[0].t)
    expect(recortada[0].t).toBe(desde)
    expect(recortada[0].v).toBeCloseTo(reales[0].v + pendiente * (desde - reales[0].t), 6)
  })

  it('un "desde" anterior al inicio o posterior al fin deja la recta completa', () => {
    const completa = lineaDelPlan(reales, 66, fin)
    expect(lineaDelPlan(reales, 66, fin, reales[0].t - DIA)).toEqual(completa)
    expect(lineaDelPlan(reales, 66, fin, fin + DIA)).toEqual(completa)
  })
})
