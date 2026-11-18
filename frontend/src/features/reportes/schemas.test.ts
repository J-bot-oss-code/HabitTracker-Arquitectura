import { describe, expect, it } from 'vitest'
import { diasDelRango, MAXIMO_DIAS_RANGO, rangoReporteSchema } from './schemas'

const mensajes = (datos: unknown) => {
  const r = rangoReporteSchema.safeParse(datos)
  return r.success ? [] : r.error.issues.map((i) => `${i.path.join('.')}: ${i.message}`)
}

describe('rangoReporteSchema (ReportesService.ValidarRango)', () => {
  it('acepta un rango válido y un solo día', () => {
    expect(rangoReporteSchema.safeParse({ desde: '2026-05-01', hasta: '2026-05-31' }).success).toBe(
      true,
    )
    expect(rangoReporteSchema.safeParse({ desde: '2026-05-05', hasta: '2026-05-05' }).success).toBe(
      true,
    )
  })

  it('desde y hasta son obligatorios', () => {
    expect(mensajes({ desde: '', hasta: '2026-05-31' })).toEqual(['desde: Indica el día inicial.'])
    expect(mensajes({ desde: '2026-05-01', hasta: '' })).toEqual(['hasta: Indica el día final.'])
    expect(mensajes({})).toHaveLength(2)
  })

  it.each([
    ['con otro formato', '01/05/2026'],
    ['con un mes que no existe', '2026-13-01'],
    ['con un día que no existe', '2026-02-30'],
  ])('rechaza una fecha %s', (_caso, valor) => {
    expect(mensajes({ desde: valor, hasta: '2026-12-31' })).toEqual([
      'desde: Elige una fecha válida.',
    ])
  })

  it('rechaza años anteriores al 2000', () => {
    expect(mensajes({ desde: '1999-12-31', hasta: '2026-01-01' })).toContain(
      'desde: Elige una fecha a partir del año 2000.',
    )
  })

  it('"hasta" no puede ser anterior a "desde"', () => {
    expect(mensajes({ desde: '2026-05-02', hasta: '2026-05-01' })).toEqual([
      'hasta: La fecha final no puede ser anterior a la inicial.',
    ])
  })

  it('admite 366 días con ambos incluidos y rechaza 367', () => {
    expect(MAXIMO_DIAS_RANGO).toBe(366)
    expect(diasDelRango('2026-01-01', '2027-01-01')).toBe(366)
    expect(rangoReporteSchema.safeParse({ desde: '2026-01-01', hasta: '2027-01-01' }).success).toBe(
      true,
    )
    expect(mensajes({ desde: '2026-01-01', hasta: '2027-01-02' })).toEqual([
      'hasta: El período no puede pasar de 366 días.',
    ])
  })

  it('cuenta bien los días de un rango que cruza un año bisiesto', () => {
    expect(diasDelRango('2028-02-28', '2028-03-01')).toBe(3)
    expect(diasDelRango('2026-05-05', '2026-05-05')).toBe(1)
  })
})
