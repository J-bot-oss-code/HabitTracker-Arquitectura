import { describe, expect, it } from 'vitest'
import { hoyUtcInput } from '@/shared/lib/fechas'
import { crearHabitoSchema, registrarAvanceSchema } from './schemas'

describe('crearHabitoSchema', () => {
  const valido = {
    nombre: 'Leer',
    periodicidad: 'Diaria',
    cantidadObjetivo: 20,
    unidad: 'páginas',
    metaId: '3f2b8c1e-6a4d-4e9b-9c7a-1d2e3f4a5b6c',
  }

  it('acepta un hábito válido', () => {
    expect(crearHabitoSchema.safeParse(valido).success).toBe(true)
  })

  it.each([
    ['nombre vacío', { nombre: '  ' }],
    ['nombre de más de 200 caracteres', { nombre: 'x'.repeat(201) }],
    ['periodicidad desconocida', { periodicidad: 'Mensual' }],
    ['objetivo cero', { cantidadObjetivo: 0 }],
    ['objetivo no numérico', { cantidadObjetivo: Number.NaN }],
    ['unidad de más de 50 caracteres', { unidad: 'u'.repeat(51) }],
    ['metaId que no es GUID', { metaId: 'meta-1' }],
  ])('rechaza %s', (_caso, cambio) => {
    expect(crearHabitoSchema.safeParse({ ...valido, ...cambio }).success).toBe(false)
  })
})

describe('registrarAvanceSchema', () => {
  it('acepta la fecha de hoy', () => {
    expect(registrarAvanceSchema.safeParse({ cantidad: 1, fecha: hoyUtcInput() }).success).toBe(
      true,
    )
  })

  it('rechaza fechas futuras', () => {
    const manana = new Date(Date.now() + 86_400_000).toISOString().slice(0, 10)
    const resultado = registrarAvanceSchema.safeParse({ cantidad: 1, fecha: manana })
    expect(resultado.success).toBe(false)
  })
})
