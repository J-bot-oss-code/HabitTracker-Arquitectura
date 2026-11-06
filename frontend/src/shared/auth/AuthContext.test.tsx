import { screen, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { emitirToken } from '@/mocks/http'
import { server } from '@/mocks/server'
import { CORREOS, renderApp } from '@/test/render'
import { tokenStorage } from './tokenStorage'

function usuarioDemo(quien: keyof typeof CORREOS) {
  const usuario = db.usuarios.find((u) => u.correo === CORREOS[quien])
  if (!usuario) throw new Error('Usuario demo inexistente')
  return usuario
}

// Token con la misma forma que el real pero que vence en `segundos` (la firma es ficticia).
function tokenQueVenceEn(quien: keyof typeof CORREOS, segundos: number): string {
  const [cabecera, carga, firma] = emitirToken(usuarioDemo(quien)).split('.')
  const claims = JSON.parse(atob(carga.replace(/-/g, '+').replace(/_/g, '/')))
  claims.exp = Math.floor(Date.now() / 1000) + segundos
  const nueva = btoa(JSON.stringify(claims))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/, '')
  return `${cabecera}.${nueva}.${firma}`
}

function contarRenovaciones() {
  const cuenta = { total: 0 }
  server.use(
    http.post('/api/auth/refresh', async ({ request }) => {
      cuenta.total += 1
      // Responde como el simulado de verdad para que la renovación funcione.
      const usuario = db.refresh && db.usuarios.find((u) => u.id === db.refresh?.usuarioId)
      if (!usuario || request.headers.get('X-HabitTracker-Csrf') !== '1') {
        return new HttpResponse(null, { status: 401 })
      }
      return HttpResponse.json({ token: emitirToken(usuario) })
    }),
  )
  return cuenta
}

describe('AuthProvider: sesión con refresh token', () => {
  it('al recargar la página restaura la sesión con la cookie, sin pedir la contraseña', async () => {
    // Antes de recargar: la persona había iniciado sesión (marca y cookie), pero el token en memoria se perdió.
    tokenStorage.guardar('jwt-de-la-pagina-anterior')
    tokenStorage.borrar()
    localStorage.setItem('habittracker.sesion', '1')
    db.refresh = {
      usuarioId: usuarioDemo('estandar').id,
      valor: 'cookie',
      sesionId: 'sesion-de-prueba',
    }

    const { router } = renderApp('/habitos')

    expect(await screen.findByRole('heading', { name: 'Mis hábitos' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/habitos')
    expect(tokenStorage.leer()).not.toBeNull()
  })

  it('si la cookie ya no sirve, la sesión no se restaura y se pide iniciar sesión', async () => {
    localStorage.setItem('habittracker.sesion', '1')
    db.refresh = null

    const { router } = renderApp('/habitos')

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(tokenStorage.huboSesion()).toBe(false)
  })

  it('una persona que nunca inició sesión no hace ninguna petición de renovación', async () => {
    const renovaciones = contarRenovaciones()

    renderApp('/')
    await screen.findByRole('link', { name: /Iniciar sesión/ })

    expect(renovaciones.total).toBe(0)
  })

  it('mientras restaura la sesión no redirige al login ni pierde la ruta pedida', async () => {
    localStorage.setItem('habittracker.sesion', '1')
    const usuario = usuarioDemo('estandar')
    db.refresh = { usuarioId: usuario.id, valor: 'cookie', sesionId: 'sesion-de-prueba' }
    // La renovación queda en espera: así se observa el estado intermedio, cuando aún no se sabe si hay sesión.
    let liberar: () => void = () => undefined
    const espera = new Promise<void>((resolver) => (liberar = resolver))
    server.use(
      http.post('/api/auth/refresh', async () => {
        await espera
        return HttpResponse.json({ token: emitirToken(usuario) })
      }),
    )

    const { router } = renderApp('/permisos')
    // La redirección de un guard sin espera ocurre de forma asíncrona: se le da tiempo a producirse.
    await new Promise((resolver) => setTimeout(resolver, 100))

    expect(router.state.location.pathname).toBe('/permisos')
    expect(screen.queryByRole('heading', { name: 'Iniciar sesión' })).not.toBeInTheDocument()

    liberar()
    expect(await screen.findByRole('heading', { name: 'Mis permisos' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/permisos')
  })

  it('renueva el token un poco antes de que venza y la sesión sigue abierta', async () => {
    const corto = tokenQueVenceEn('estandar', 30)
    db.refresh = {
      usuarioId: usuarioDemo('estandar').id,
      valor: 'cookie',
      sesionId: 'sesion-de-prueba',
    }
    const renovaciones = contarRenovaciones()
    tokenStorage.guardar(corto)

    renderApp('/habitos')

    await waitFor(() => expect(renovaciones.total).toBe(1))
    expect(await screen.findByRole('heading', { name: 'Mis hábitos' })).toBeInTheDocument()
    await waitFor(() => expect(tokenStorage.leer()).not.toBe(corto))
  })

  it('si la renovación falla antes del vencimiento, cierra la sesión con un aviso', async () => {
    db.refresh = null
    tokenStorage.guardar(tokenQueVenceEn('estandar', 30))

    const { router } = renderApp('/habitos')

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(screen.getByText(/Tu sesión expiró o fue cerrada/)).toBeInTheDocument()
  })
})
