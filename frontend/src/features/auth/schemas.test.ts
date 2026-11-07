import { describe, expect, it } from 'vitest'
import {
  loginSchema,
  nombreCompletoSchema,
  passwordNuevaSchema,
  registroSchema,
  REQUISITOS_CLAVE,
} from './schemas'
import { estadoConfirmacion } from './validacionEnVivo'

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
      nombre: 'Ana Pérez',
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

describe('nombreCompletoSchema', () => {
  it.each([
    'Ana Pérez',
    'María José Núñez',
    "Liam O'Neill",
    'Ana Pérez-Gil',
    '  Juan   Ramírez  ',
    'Ana P.',
    "Gerard 't Hooft",
  ])('acepta %s', (nombre) => {
    expect(nombreCompletoSchema.safeParse(nombre).success).toBe(true)
  })

  it.each([
    ['vacío', '', 'El nombre es obligatorio.'],
    ['una sola palabra', 'Ana', 'Escribe tu nombre y tu apellido.'],
    ['números', 'Ana P3rez', 'Usa solo letras, espacios, apóstrofos, guiones o puntos.'],
    ['símbolos', 'Ana <Pérez>', 'Usa solo letras, espacios, apóstrofos, guiones o puntos.'],
  ])('rechaza un nombre %s', (_caso, nombre, mensaje) => {
    const resultado = nombreCompletoSchema.safeParse(nombre)
    expect(resultado.success).toBe(false)
    expect(resultado.error?.issues[0].message).toBe(mensaje)
  })
})

describe('REQUISITOS_CLAVE (lista en vivo)', () => {
  it('marca cada requisito por separado', () => {
    const cumplidos = (clave: string) =>
      REQUISITOS_CLAVE.filter((r) => r.cumple(clave)).map((r) => r.id)
    expect(cumplidos('')).toEqual([])
    expect(cumplidos('abc')).toEqual(['letra'])
    expect(cumplidos('abc1')).toEqual(['letra', 'numero'])
    expect(cumplidos(CLAVE_VALIDA)).toEqual(['longitud', 'letra', 'numero'])
  })

  it('coincide con el esquema: todos cumplidos equivale a una clave válida', () => {
    for (const clave of ['', 'abc1', '123456789012', 'SoloLetrasLargas', CLAVE_VALIDA]) {
      const todos = REQUISITOS_CLAVE.every((r) => r.cumple(clave))
      expect(passwordNuevaSchema.safeParse(clave).success).toBe(todos)
    }
  })
})

describe('estadoConfirmacion', () => {
  it('no dice nada mientras la confirmación está vacía', () => {
    expect(estadoConfirmacion(CLAVE_VALIDA, '')).toEqual({ valido: false })
  })

  it('avisa si aún no coincide y confirma cuando coincide', () => {
    expect(estadoConfirmacion(CLAVE_VALIDA, 'Clave')).toEqual({
      valido: false,
      ayuda: 'Aún no coincide con la contraseña.',
    })
    expect(estadoConfirmacion(CLAVE_VALIDA, CLAVE_VALIDA)).toEqual({
      valido: true,
      ayuda: 'Las contraseñas coinciden.',
    })
  })

  it('no la marca como válida si la contraseña no cumple la política', () => {
    expect(estadoConfirmacion('corta1', 'corta1').valido).toBe(false)
  })
})
