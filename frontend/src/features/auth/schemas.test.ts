import { describe, expect, it } from 'vitest'
import { loginSchema, passwordNuevaSchema, registroSchema } from './schemas'

describe('passwordNuevaSchema (RF-CA-14)', () => {
  it.each(['Clave-Segura-26', 'contraseña-larga9', 'Ñandú-Segura2026'])('acepta %s', (clave) => {
    expect(passwordNuevaSchema.safeParse(clave).success).toBe(true)
  })

  it.each([
    ['menos de 12 caracteres', 'Ab1'],
    ['11 caracteres (el mínimo es 12)', 'Clave123456'],
    ['sin números', 'SoloLetrasLargas'],
    ['sin letras', '123456789012'],
  ])('rechaza una clave con %s', (_caso, clave) => {
    expect(passwordNuevaSchema.safeParse(clave).success).toBe(false)
  })
})

const CLAVE_VALIDA = 'Clave-Segura-2026'

describe('registroSchema', () => {
  it('exige que la confirmación coincida', () => {
    const resultado = registroSchema.safeParse({
      nombre: 'Ana',
      correo: 'ana@demo.com',
      password: CLAVE_VALIDA,
      confirmacion: 'Clave-Segura-2027',
    })
    expect(resultado.success).toBe(false)
    expect(resultado.error?.issues[0].path).toEqual(['confirmacion'])
  })
})

describe('loginSchema', () => {
  it('no aplica la política de clave al iniciar sesión', () => {
    expect(loginSchema.safeParse({ email: 'ana@demo.com', password: 'x' }).success).toBe(true)
  })

  it('rechaza un correo mal formado', () => {
    expect(loginSchema.safeParse({ email: 'ana', password: 'x' }).success).toBe(false)
  })
})
