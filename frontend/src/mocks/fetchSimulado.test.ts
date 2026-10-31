import { describe, expect, it, vi } from 'vitest'
import { PASSWORD_DEMO } from './db'
import { crearFetchSimulado } from './fetchSimulado'

const ORIGEN = 'http://demo.local'

describe('fetchSimulado (modo demo sin Service Worker)', () => {
  it('resuelve /api/auth/login con los handlers de la API simulada', async () => {
    const fetchReal = vi.fn()
    const fetchSimulado = crearFetchSimulado(fetchReal, ORIGEN)
    const respuesta = await fetchSimulado(`${ORIGEN}/api/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'estandar@demo.com', password: PASSWORD_DEMO }),
    })
    expect(respuesta.status).toBe(200)
    expect((await respuesta.json()).token).toEqual(expect.any(String))
    expect(fetchReal).not.toHaveBeenCalled()
  })

  it('deja pasar al fetch real lo que no es /api del propio origen', async () => {
    const fetchReal = vi.fn(async () => new Response('ok'))
    const fetchSimulado = crearFetchSimulado(fetchReal, ORIGEN)
    await fetchSimulado(`${ORIGEN}/assets/logo.svg`)
    await fetchSimulado('https://otro.example/api/auth/login')
    expect(fetchReal).toHaveBeenCalledTimes(2)
  })

  it('responde 404 a una ruta /api que el backend no tiene', async () => {
    const fetchSimulado = crearFetchSimulado(vi.fn(), ORIGEN)
    expect((await fetchSimulado(`${ORIGEN}/api/no-existe`)).status).toBe(404)
  })
})
