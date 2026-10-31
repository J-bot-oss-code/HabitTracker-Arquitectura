import { describe, expect, it } from 'vitest'
import { iniciarSesionComo } from '@/test/render'

// La API simulada debe responder como Host/WebApi. Estas pruebas fijan las reglas del contrato
// que la interfaz da por supuestas; si el backend cambia, se actualizan aquí primero.
const url = (ruta: string) => `${window.location.origin}${ruta}`

async function llamar(
  ruta: string,
  opciones: { metodo?: string; token?: string; body?: unknown } = {},
) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (opciones.token) headers.Authorization = `Bearer ${opciones.token}`
  return fetch(url(ruta), {
    method: opciones.metodo ?? 'GET',
    headers,
    body: opciones.body === undefined ? undefined : JSON.stringify(opciones.body),
  })
}

const LEER = '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e01'
const CORRER = '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e02'

describe('contrato de la API simulada', () => {
  it('el login usa "email" (no "correo")', async () => {
    const conCorreo = await llamar('/api/auth/login', {
      metodo: 'POST',
      body: { correo: 'estandar@demo.com', password: 'Demo1234' },
    })
    expect(conCorreo.status).toBe(400)
    const conEmail = await llamar('/api/auth/login', {
      metodo: 'POST',
      body: { email: 'estandar@demo.com', password: 'Demo1234' },
    })
    expect(conEmail.status).toBe(200)
    expect((await conEmail.json()).token).toEqual(expect.any(String))
  })

  it('sin token responde 401', async () => {
    expect((await llamar('/api/habits')).status).toBe(401)
  })

  it('api/habits es solo para Estándar: el Administrador recibe 403', async () => {
    expect((await llamar('/api/habits', { token: iniciarSesionComo('admin') })).status).toBe(403)
  })

  it('los enums viajan como texto y las fechas de hábitos sin "Z"', async () => {
    const r = await llamar('/api/habits', { token: iniciarSesionComo('estandar') })
    const [habito] = await r.json()
    expect(habito.periodicidad).toBe('Diaria')
    expect(habito.estado).toBe('Activo')
    expect(habito.fechaInicioCiclo).not.toMatch(/Z$/)
  })

  it('Planificado → Completado está prohibido', async () => {
    const r = await llamar(`/api/habits/${CORRER}/transition`, {
      metodo: 'POST',
      token: iniciarSesionComo('estandar'),
      body: { nuevoEstado: 'Completado' },
    })
    expect(r.status).toBe(400)
    const problema = await r.json()
    expect(problema.title).toBe('La operación no pudo completarse.')
  })

  it('un hábito de otro usuario responde 403 y uno inexistente 404', async () => {
    const token = iniciarSesionComo('otro')
    const ajeno = await llamar(`/api/habits/${LEER}/transition`, {
      metodo: 'POST',
      token,
      body: { nuevoEstado: 'Pausado' },
    })
    expect(ajeno.status).toBe(403)
    const inexistente = await llamar(
      '/api/habits/00000000-0000-0000-0000-000000000000/transition',
      {
        metodo: 'POST',
        token,
        body: { nuevoEstado: 'Pausado' },
      },
    )
    expect(inexistente.status).toBe(404)
  })

  it('el avance exige fecha UTC con "Z"', async () => {
    const token = iniciarSesionComo('estandar')
    const sinZ = await llamar(`/api/habits/${LEER}/progress`, {
      metodo: 'POST',
      token,
      body: { cantidad: 1, fechaUtc: '2026-10-30T00:00:00' },
    })
    expect(sinZ.status).toBe(400)
    const conZ = await llamar(`/api/habits/${LEER}/progress`, {
      metodo: 'POST',
      token,
      body: { cantidad: 1, fechaUtc: new Date().toISOString() },
    })
    expect(conZ.status).toBe(204)
  })

  it('GET /api/permissions devuelve solo pendientes si se omite soloPendientes', async () => {
    const token = iniciarSesionComo('admin')
    await llamar('/api/permissions/5a4b3c2d-1e0f-4a9b-8c7d-6e5f4a3b2c01/reject', {
      metodo: 'POST',
      token,
      body: { motivoRechazo: 'No' },
    })
    expect(await (await llamar('/api/permissions', { token })).json()).toHaveLength(0)
    expect(
      await (await llamar('/api/permissions?soloPendientes=false', { token })).json(),
    ).toHaveLength(1)
  })

  it('el logout revoca el token (blacklist)', async () => {
    const token = iniciarSesionComo('estandar')
    expect((await llamar('/api/auth/logout', { metodo: 'POST', token })).status).toBe(200)
    expect((await llamar('/api/auth/me', { token })).status).toBe(401)
  })
})
