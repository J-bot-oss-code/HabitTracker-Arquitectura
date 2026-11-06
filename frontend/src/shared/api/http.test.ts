import { http as mswHttp, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import { server } from '@/mocks/server'
import { tokenStorage } from '@/shared/auth/tokenStorage'
import { alRecibirNoAutorizado, api } from './http'
import { ApiError } from './problem'

function contarRenovaciones() {
  const cuenta = { total: 0 }
  server.use(
    mswHttp.post('/api/auth/refresh', () => {
      cuenta.total += 1
      return HttpResponse.json({ token: 'otro' })
    }),
  )
  return cuenta
}

describe('cliente HTTP: renovación de la sesión con el refresh token', () => {
  it('envía siempre las credenciales (la cookie del refresh solo viaja a /api/auth)', async () => {
    let credenciales: string | null = null
    server.use(
      mswHttp.get('/api/prueba', ({ request }) => {
        credenciales = request.credentials
        return HttpResponse.json({ ok: true })
      }),
    )

    await api.get('/api/prueba')

    expect(credenciales).toBe('include')
  })

  it('con el token vencido (401) renueva UNA vez y repite la petición con el token nuevo', async () => {
    tokenStorage.guardar('jwt-vencido')
    const usados: (string | null)[] = []
    server.use(
      mswHttp.get('/api/prueba', ({ request }) => {
        const auth = request.headers.get('Authorization')
        usados.push(auth)
        return auth === 'Bearer jwt-nuevo'
          ? HttpResponse.json({ ok: true })
          : new HttpResponse(null, { status: 401 })
      }),
      mswHttp.post('/api/auth/refresh', () => HttpResponse.json({ token: 'jwt-nuevo' })),
    )
    const alNoAutorizado = vi.fn()
    const baja = alRecibirNoAutorizado(alNoAutorizado)

    const respuesta = await api.get<{ ok: boolean }>('/api/prueba')
    baja()

    expect(respuesta.ok).toBe(true)
    expect(usados).toEqual(['Bearer jwt-vencido', 'Bearer jwt-nuevo'])
    expect(tokenStorage.leer()).toBe('jwt-nuevo')
    expect(alNoAutorizado).not.toHaveBeenCalled()
  })

  it('si ya no se puede renovar (sesión revocada), cierra la sesión', async () => {
    tokenStorage.guardar('jwt-revocado')
    server.use(
      mswHttp.get('/api/prueba', () => new HttpResponse(null, { status: 401 })),
      mswHttp.post('/api/auth/refresh', () => new HttpResponse(null, { status: 401 })),
    )
    const alNoAutorizado = vi.fn()
    const baja = alRecibirNoAutorizado(alNoAutorizado)

    await expect(api.get('/api/prueba')).rejects.toBeInstanceOf(ApiError)
    baja()

    expect(alNoAutorizado).toHaveBeenCalledTimes(1)
  })

  it('un 401 del propio login (credenciales incorrectas) no intenta renovar ni cierra nada', async () => {
    tokenStorage.guardar('jwt-vigente')
    server.use(mswHttp.post('/api/auth/login', () => new HttpResponse(null, { status: 401 })))
    const renovaciones = contarRenovaciones()
    const alNoAutorizado = vi.fn()
    const baja = alRecibirNoAutorizado(alNoAutorizado)

    await expect(api.post('/api/auth/login', {})).rejects.toBeInstanceOf(ApiError)
    baja()

    expect(renovaciones.total).toBe(0)
    expect(alNoAutorizado).not.toHaveBeenCalled()
  })

  it('sin token no se intenta renovar: un 401 anónimo no cierra una sesión que no existe', async () => {
    server.use(mswHttp.get('/api/prueba', () => new HttpResponse(null, { status: 401 })))
    const renovaciones = contarRenovaciones()

    await expect(api.get('/api/prueba')).rejects.toBeInstanceOf(ApiError)

    expect(renovaciones.total).toBe(0)
  })

  it('un 403 no es una sesión vencida: no se renueva', async () => {
    tokenStorage.guardar('jwt-vigente')
    server.use(mswHttp.get('/api/prueba', () => new HttpResponse(null, { status: 403 })))
    const renovaciones = contarRenovaciones()

    await expect(api.get('/api/prueba')).rejects.toMatchObject({ status: 403 })

    expect(renovaciones.total).toBe(0)
  })

  it('la petición repetida conserva el cuerpo JSON', async () => {
    tokenStorage.guardar('jwt-vencido')
    const cuerpos: unknown[] = []
    server.use(
      mswHttp.post('/api/prueba', async ({ request }) => {
        cuerpos.push(await request.json())
        return request.headers.get('Authorization') === 'Bearer jwt-nuevo'
          ? HttpResponse.json({ ok: true })
          : new HttpResponse(null, { status: 401 })
      }),
      mswHttp.post('/api/auth/refresh', () => HttpResponse.json({ token: 'jwt-nuevo' })),
    )

    await api.post('/api/prueba', { dato: 1 })

    expect(cuerpos).toEqual([{ dato: 1 }, { dato: 1 }])
  })
})
