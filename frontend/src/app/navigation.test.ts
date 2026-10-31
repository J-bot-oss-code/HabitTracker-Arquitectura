import { describe, expect, it } from 'vitest'
import { rutaPermitida } from './navigation'

describe('rutaPermitida', () => {
  it.each([
    ['Estandar', '/habitos', true],
    ['Estandar', '/habitos/123?x=1', true],
    ['Estandar', '/permisos', true],
    ['Estandar', '/admin/usuarios', false],
    ['Administrador', '/habitos', false],
    ['Administrador', '/permisos', false],
    ['Administrador', '/admin/permisos', true],
    ['Administrador', '/permisos/abc', true],
    ['Estandar', '/cuenta', true],
    ['Administrador', '/reportes', true],
  ] as const)('%s → %s: %s', (rol, ruta, esperado) => {
    expect(rutaPermitida(rol, ruta)).toBe(esperado)
  })
})
