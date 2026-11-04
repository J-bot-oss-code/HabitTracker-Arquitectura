import { describe, expect, it } from 'vitest'
import { calcularKcal, metabolismoBasal } from './calorias'
import { AREAS, buscarMeta, METAS_CATALOGO, metasDelArea } from './catalogo'
import { esDocumentoLegal, INFO_LEGAL, VERSION_PRIVACIDAD, VERSION_TERMINOS } from './legal'
import { calculadoraSchema } from './schemas'

describe('catálogo de metas', () => {
  it('contiene las seis metas de la especificación con slugs únicos', () => {
    expect(METAS_CATALOGO).toHaveLength(6)
    expect(new Set(METAS_CATALOGO.map((m) => m.slug)).size).toBe(6)
  })

  it('cada área del filtro tiene al menos una meta', () => {
    for (const area of AREAS) expect(metasDelArea(area).length).toBeGreaterThan(0)
    expect(metasDelArea('todas')).toHaveLength(6)
    expect(metasDelArea('Descanso').map((m) => m.nombre)).toEqual(['Dormir mejor'])
  })

  it('buscarMeta valida el slug que llega de la URL', () => {
    expect(buscarMeta('primeros-5-km')?.nombre).toBe('Primeros 5 km')
    expect(buscarMeta('<script>')).toBeNull()
    expect(buscarMeta(null)).toBeNull()
    expect(buscarMeta('')).toBeNull()
  })

  it('la duración en semanas cuadra con el texto (21 días = 3 semanas)', () => {
    expect(buscarMeta('reducir-estres')).toMatchObject({ semanas: 3, duracion: '21 días' })
  })
})

describe('calculadora de calorías (Mifflin-St Jeor)', () => {
  it('calcula el metabolismo basal de mujer y hombre', () => {
    const base = { edad: 30, alturaCm: 165, pesoKg: 60, actividad: 'moderada' } as const
    expect(metabolismoBasal({ ...base, sexo: 'mujer' })).toBeCloseTo(1320.25)
    expect(metabolismoBasal({ ...base, sexo: 'hombre' })).toBeCloseTo(1486.25)
  })

  it('multiplica por el factor de actividad y redondea a la decena', () => {
    // 1320,25 × 1,55 = 2046,4 → 2050
    expect(
      calcularKcal({ edad: 30, sexo: 'mujer', alturaCm: 165, pesoKg: 60, actividad: 'moderada' }),
    ).toBe(2050)
    // (800 + 1125 − 125 + 5) × 1,2 = 2166 → 2170
    expect(
      calcularKcal({
        edad: 25,
        sexo: 'hombre',
        alturaCm: 180,
        pesoKg: 80,
        actividad: 'sedentaria',
      }),
    ).toBe(2170)
  })
})

describe('validación de la calculadora', () => {
  const validos = { edad: 30, sexo: 'mujer', alturaCm: 165, pesoKg: 60, actividad: 'ligera' }

  it('acepta datos dentro de los límites', () => {
    expect(calculadoraSchema.safeParse(validos).success).toBe(true)
  })

  it.each([
    ['edad', 15],
    ['edad', 101],
    ['edad', 30.5],
    ['edad', Number.NaN],
    ['alturaCm', 119],
    ['pesoKg', 301],
    ['sexo', ''],
    ['actividad', 'extrema'],
  ])('rechaza %s = %s', (campo, valor) => {
    expect(calculadoraSchema.safeParse({ ...validos, [campo]: valor }).success).toBe(false)
  })
})

describe('documentos legales', () => {
  it('versiona términos y privacidad como indica la especificación', () => {
    expect(VERSION_TERMINOS).toBe('v2.1')
    expect(VERSION_PRIVACIDAD).toBe('v1.4')
    expect(INFO_LEGAL.terminos.version).toBe(VERSION_TERMINOS)
  })

  it('solo reconoce los documentos publicados', () => {
    expect(esDocumentoLegal('salud')).toBe(true)
    expect(esDocumentoLegal('../secreto')).toBe(false)
  })
})
