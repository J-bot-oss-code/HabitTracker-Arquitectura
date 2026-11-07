import { screen, waitFor, within } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { server } from '@/mocks/server'
import { iniciarSesionComo, renderApp } from '@/test/render'

const ana = () => db.usuarios[0]
const FIREFOX = '5e5510a0-0000-4000-8000-000000000001'
const SAFARI = '5e5510a0-0000-4000-8000-000000000002'

// Deja a este navegador con una sesión abierta (la de la cookie del refresh token) además de las dos de la semilla.
function abrirSesionActual() {
  const ahora = new Date().toISOString()
  db.sesiones.push({
    id: 'sesion-actual',
    usuarioId: ana().id,
    dispositivo: 'Este navegador (demostración)',
    creadaEn: ahora,
    ultimoUsoEn: ahora,
  })
  db.refresh = { usuarioId: ana().id, valor: 'refresh-de-prueba', sesionId: 'sesion-actual' }
}

async function abrirPanel() {
  const vista = renderApp('/ajustes/inicio-sesion', { como: 'estandar' })
  const titulo = await screen.findByRole('heading', { name: 'Sesiones activas' })
  const tarjeta = titulo.closest('div')?.parentElement as HTMLElement
  return { ...vista, tarjeta }
}

describe('Sesiones activas (Configuración › Inicio de sesión)', () => {
  it('lista los dispositivos y marca el de este navegador', async () => {
    abrirSesionActual()
    await abrirPanel()

    expect(await screen.findByText('Firefox en Windows')).toBeInTheDocument()
    expect(screen.getByText('Safari en iPhone')).toBeInTheDocument()
    expect(screen.getByText('Este navegador (demostración)')).toBeInTheDocument()
    // Solo la sesión actual lleva la marca, y no se puede cerrar desde su propia fila.
    expect(screen.getAllByText('Este dispositivo')).toHaveLength(1)
    expect(screen.queryByRole('button', { name: /Cerrar la sesión de Este navegador/ })).toBeNull()
  })

  it('cierra una sola sesión y deja las demás', async () => {
    abrirSesionActual()
    const { user } = await abrirPanel()

    await user.click(
      await screen.findByRole('button', { name: 'Cerrar la sesión de Firefox en Windows' }),
    )

    expect(await screen.findByText('Se cerró esa sesión.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByText('Firefox en Windows')).not.toBeInTheDocument())
    expect(screen.getByText('Safari en iPhone')).toBeInTheDocument()
    expect(db.sesiones.map((s) => s.id)).toEqual(expect.arrayContaining([SAFARI, 'sesion-actual']))
    expect(db.sesiones.some((s) => s.id === FIREFOX)).toBe(false)
  })

  it('«Cerrar las demás» conserva solo la de este navegador', async () => {
    abrirSesionActual()
    const { user } = await abrirPanel()

    await user.click(await screen.findByRole('button', { name: 'Cerrar las demás' }))

    expect(await screen.findByText('Se cerraron las demás sesiones.')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByText('Safari en iPhone')).not.toBeInTheDocument())
    expect(db.sesiones.map((s) => s.id)).toEqual(['sesion-actual'])
    // Sin otras sesiones ya no se ofrece el botón.
    expect(screen.queryByRole('button', { name: 'Cerrar las demás' })).toBeNull()
  })

  it('«Cerrar en todos los dispositivos» pide confirmar, cierra también este navegador y lo dice en el login', async () => {
    abrirSesionActual()
    const { user, router } = await abrirPanel()

    await user.click(
      await screen.findByRole('button', { name: 'Cerrar en todos los dispositivos' }),
    )
    const dialogo = await screen.findByRole('dialog')
    // Cancelar no cierra nada.
    await user.click(within(dialogo).getByRole('button', { name: 'Cancelar' }))
    expect(db.sesiones).toHaveLength(3)

    await user.click(screen.getByRole('button', { name: 'Cerrar en todos los dispositivos' }))
    await user.click(
      within(await screen.findByRole('dialog')).getByRole('button', { name: 'Cerrar en todos' }),
    )

    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
    expect(screen.getByText('Cerraste la sesión en todos los dispositivos.')).toBeInTheDocument()
    expect(db.sesiones.filter((s) => s.usuarioId === ana().id)).toHaveLength(0)
    expect(db.refresh).toBeNull()
  })

  it('si el servidor falla al cerrar una sesión lo avisa y la sesión sigue en la lista', async () => {
    abrirSesionActual()
    server.use(
      http.delete('/api/auth/sesiones/:id', () =>
        HttpResponse.json(
          { title: 'Error', detail: 'Error interno.', status: 500 },
          { status: 500 },
        ),
      ),
    )
    const { user } = await abrirPanel()

    await user.click(
      await screen.findByRole('button', { name: 'Cerrar la sesión de Safari en iPhone' }),
    )

    expect(await screen.findByRole('alert')).toBeInTheDocument()
    expect(screen.getByText('Safari en iPhone')).toBeInTheDocument()
  })

  it('si la sesión ya estaba cerrada (404) avisa y la quita de la lista', async () => {
    abrirSesionActual()
    server.use(
      http.delete('/api/auth/sesiones/:id', () => {
        // Otro dispositivo la cerró entre tanto.
        db.sesiones = db.sesiones.filter((s) => s.id !== SAFARI)
        return HttpResponse.json(
          { title: 'No encontrado', detail: 'La sesión no existe.', status: 404 },
          { status: 404 },
        )
      }),
    )
    const { user } = await abrirPanel()

    await user.click(
      await screen.findByRole('button', { name: 'Cerrar la sesión de Safari en iPhone' }),
    )

    expect(await screen.findByRole('alert')).toBeInTheDocument()
    await waitFor(() => expect(screen.queryByText('Safari en iPhone')).not.toBeInTheDocument())
  })

  it('el nombre del dispositivo se muestra como texto, nunca como HTML', async () => {
    abrirSesionActual()
    db.sesiones.find((s) => s.id === FIREFOX)!.dispositivo =
      '<img src=x onerror=alert(1)> Navegador'
    await abrirPanel()

    expect(await screen.findByText('<img src=x onerror=alert(1)> Navegador')).toBeInTheDocument()
    expect(document.querySelector('img[src="x"]')).toBeNull()
  })
})

describe('contrato simulado de /api/auth/sesiones', () => {
  const llamar = (ruta: string, token: string, metodo = 'GET') =>
    fetch(`${window.location.origin}${ruta}`, {
      method: metodo,
      headers: { Authorization: `Bearer ${token}` },
    })

  it('exige autenticación', async () => {
    expect((await fetch(`${window.location.origin}/api/auth/sesiones`)).status).toBe(401)
    expect(
      (await fetch(`${window.location.origin}/api/auth/logout-todas`, { method: 'POST' })).status,
    ).toBe(401)
  })

  it('devuelve solo las sesiones propias, ordenadas por último uso, con `actual`', async () => {
    abrirSesionActual()
    // Una sesión de otra persona nunca se ve ni se cierra.
    db.sesiones.push({
      id: 'ajena',
      usuarioId: db.usuarios[1].id,
      dispositivo: 'Equipo de otra persona',
      creadaEn: new Date().toISOString(),
      ultimoUsoEn: new Date().toISOString(),
    })
    const token = iniciarSesionComo('estandar')

    const lista = (await (await llamar('/api/auth/sesiones', token)).json()) as {
      id: string
      actual: boolean
    }[]
    expect(lista.map((s) => s.id)).not.toContain('ajena')
    expect(lista.filter((s) => s.actual).map((s) => s.id)).toEqual(['sesion-actual'])
    expect(lista.at(-1)?.id).toBe(FIREFOX)

    expect((await llamar('/api/auth/sesiones/ajena', token, 'DELETE')).status).toBe(404)
    expect((await llamar('/api/auth/sesiones/no-existe', token, 'DELETE')).status).toBe(404)
    expect(db.sesiones.some((s) => s.id === 'ajena')).toBe(true)
    expect((await llamar(`/api/auth/sesiones/${FIREFOX}`, token, 'DELETE')).status).toBe(204)
  })

  it('logout-todas corta el acceso ya emitido', async () => {
    const token = iniciarSesionComo('estandar')
    // El corte vale desde el milisegundo de la revocación: el token se emitió antes.
    await new Promise((r) => setTimeout(r, 5))
    expect((await llamar('/api/auth/logout-todas', token, 'POST')).status).toBe(200)
    expect((await llamar('/api/auth/me', token)).status).toBe(401)
    expect(db.sesiones.filter((s) => s.usuarioId === ana().id)).toHaveLength(0)
  })
})
