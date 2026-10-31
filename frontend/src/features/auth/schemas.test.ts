import { describe, expect, it } from 'vitest'
import { loginSchema, passwordNuevaSchema, registroSchema } from './schemas'

describe('passwordNuevaSchema (RF-CA-14)', () => {
  it.each(['Clave1234', 'contraseña9', 'Ñandú2026'])('acepta %s', (clave) => {
    expect(passwordNuevaSchema.safeParse(clave).success).toBe(true)
  })

  it.each([
    ['menos de 8 caracteres', 'Ab1'],
    ['sin números', 'SoloLetras'],
    ['sin letras', '12345678'],
  ])('rechaza una clave con %s', (_caso, clave) => {
    expect(passwordNuevaSchema.safeParse(clave).success).toBe(false)
  })
})

describe('registroSchema', () => {
  it('exige que la confirmación coincida', () => {
    const resultado = registroSchema.safeParse({
      nombre: 'Ana',
      correo: 'ana@demo.com',
      password: 'Clave1234',
      confirmacion: 'Clave12345',
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
