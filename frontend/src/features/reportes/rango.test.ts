import { describe, expect, it } from 'vitest'
import { rangoUltimosDias } from './rango'
import { diasDelRango, rangoReporteSchema } from './schemas'

describe('rangoUltimosDias', () => {
  const ahora = new Date('2026-03-02T10:30:00Z')

  it('termina hoy (UTC) e incluye ambos extremos', () => {
    expect(rangoUltimosDias(7, ahora)).toEqual({ desde: '2026-02-24', hasta: '2026-03-02' })
    expect(diasDelRango('2026-02-24', '2026-03-02')).toBe(7)
    expect(rangoUltimosDias(1, ahora)).toEqual({ desde: '2026-03-02', hasta: '2026-03-02' })
  })

  it('siempre produce un rango que el esquema acepta', () => {
    for (const dias of [7, 30, 366]) {
      expect(rangoReporteSchema.safeParse(rangoUltimosDias(dias, ahora)).success).toBe(true)
    }
  })
})
