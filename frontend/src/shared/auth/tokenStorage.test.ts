import { describe, expect, it } from 'vitest'
import { tokenStorage } from './tokenStorage'

describe('tokenStorage (RD-10: el token de acceso solo vive en memoria)', () => {
  it('guarda y devuelve el token', () => {
    tokenStorage.guardar('jwt-de-prueba')
    expect(tokenStorage.leer()).toBe('jwt-de-prueba')
  })

  it('nunca escribe el token en sessionStorage ni en localStorage', () => {
    tokenStorage.guardar('jwt-secreto-que-no-debe-persistir')

    const volcado = JSON.stringify({ ...sessionStorage }) + JSON.stringify({ ...localStorage })
    expect(volcado).not.toContain('jwt-secreto-que-no-debe-persistir')
  })

  it('deja una marca no sensible para saber que hubo una sesión en este navegador', () => {
    expect(tokenStorage.huboSesion()).toBe(false)

    tokenStorage.guardar('jwt-de-prueba')

    expect(tokenStorage.huboSesion()).toBe(true)
    expect(localStorage.getItem('habittracker.sesion')).toBe('1')
  })

  it('borrar quita el token y la marca', () => {
    tokenStorage.guardar('jwt-de-prueba')

    tokenStorage.borrar()

    expect(tokenStorage.leer()).toBeNull()
    expect(tokenStorage.huboSesion()).toBe(false)
  })
})
