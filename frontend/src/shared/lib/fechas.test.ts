import { describe, expect, it } from 'vitest'
import { etiquetaDiaSemanaUtc, fechaInputAUtcIso, hoyUtcInput, parsearFechaUtc } from './fechas'

describe('fechas en UTC (RD-11)', () => {
  it('interpreta como UTC una fecha del backend que llega sin "Z"', () => {
    expect(parsearFechaUtc('2026-10-28T14:03:22.1234567').toISOString()).toBe(
      '2026-10-28T14:03:22.123Z',
    )
  })

  it('respeta una fecha que ya trae zona', () => {
    expect(parsearFechaUtc('2026-10-28T14:00:00Z').toISOString()).toBe('2026-10-28T14:00:00.000Z')
  })

  it('convierte el valor de un input date a ISO UTC con "Z"', () => {
    const ahora = new Date('2026-11-05T15:00:00Z')
    expect(fechaInputAUtcIso('2026-10-30', ahora)).toBe('2026-10-30T00:00:00.000Z')
  })

  it('para el día de hoy envía el instante actual (cuenta en un ciclo que empezó hoy)', () => {
    const ahora = new Date('2026-10-30T15:20:00Z')
    expect(fechaInputAUtcIso('2026-10-30', ahora)).toBe('2026-10-30T15:20:00.000Z')
  })

  it('calcula el día de hoy en UTC', () => {
    expect(hoyUtcInput(new Date('2026-10-30T23:30:00-05:00'))).toBe('2026-10-31')
  })
})

describe('etiquetaDiaSemanaUtc', () => {
  it('usa el día UTC aunque la zona local sea otra', () => {
    expect(etiquetaDiaSemanaUtc(new Date('2026-10-26T23:30:00Z'))).toMatch(/lun/i)
    expect(etiquetaDiaSemanaUtc(new Date('2026-10-27T00:30:00Z'))).toMatch(/mar/i)
  })
})
