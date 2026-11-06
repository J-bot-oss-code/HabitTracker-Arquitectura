import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { server } from '@/mocks/server'
import { tokenStorage } from '@/shared/auth/tokenStorage'
import { alRenovarToken, CABECERA_CSRF, renovarToken } from './sesion'

describe('renovarToken (refresh token en cookie HttpOnly)', () => {
  it('pide la renovación con la cabecera anti-CSRF y enviando la cookie', async () => {
    let cabecera: string | null = null
    let credenciales: string | null = null
    server.use(
      http.post('/api/auth/refresh', ({ request }) => {
        cabecera = request.headers.get(CABECERA_CSRF)
        credenciales = request.credentials
        return HttpResponse.json({ token: 'jwt-nuevo' })
      }),
    )

    await renovarToken()

    expect(cabecera).toBe('1')
    expect(credenciales).toBe('include')
  })

  it('guarda el token nuevo y avisa a quien escucha', async () => {
    server.use(http.post('/api/auth/refresh', () => HttpResponse.json({ token: 'jwt-nuevo' })))
    const oyente = vi.fn()
    const darDeBaja = alRenovarToken(oyente)

    const token = await renovarToken()
    darDeBaja()

    expect(token).toBe('jwt-nuevo')
    expect(tokenStorage.leer()).toBe('jwt-nuevo')
    expect(oyente).toHaveBeenCalledWith('jwt-nuevo')
  })

  it('devuelve null y no toca el token si ya no hay sesión (401)', async () => {
    server.use(http.post('/api/auth/refresh', () => new HttpResponse(null, { status: 401 })))
    tokenStorage.guardar('jwt-viejo')

    expect(await renovarToken()).toBeNull()
    expect(tokenStorage.leer()).toBe('jwt-viejo')
  })

  it('devuelve null si el servidor no responde la renovación', async () => {
    server.use(http.post('/api/auth/refresh', () => HttpResponse.error()))

    expect(await renovarToken()).toBeNull()
  })

  it('varias renovaciones simultáneas comparten UNA sola petición (dos con el mismo refresh token cerrarían la sesión)', async () => {
    let peticiones = 0
    server.use(
      http.post('/api/auth/refresh', () => {
        peticiones += 1
        return HttpResponse.json({ token: `jwt-${peticiones}` })
      }),
    )

    const resultados = await Promise.all([renovarToken(), renovarToken(), renovarToken()])

    expect(peticiones).toBe(1)
    expect(new Set(resultados).size).toBe(1)
  })

  it('una renovación posterior vuelve a pedirse (no queda en caché)', async () => {
    let peticiones = 0
    server.use(
      http.post('/api/auth/refresh', () => {
        peticiones += 1
        return HttpResponse.json({ token: `jwt-${peticiones}` })
      }),
    )

    await renovarToken()
    await renovarToken()

    expect(peticiones).toBe(2)
  })
})
