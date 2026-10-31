import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { iniciarSesionComo, renderApp, renderAppConEstado } from '@/test/render'

describe('rutas protegidas y navegación por rol', () => {
  it('sin sesión, una ruta protegida lleva al login', async () => {
    const { router } = renderApp('/habitos')
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('tras iniciar sesión vuelve a la ruta que se pidió', async () => {
    const { user, router } = renderApp('/permisos')
    await user.type(await screen.findByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'Demo1234')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByRole('heading', { name: 'Mis permisos' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/permisos')
  })

  it('no redirige fuera de la app aunque el destino previo lo intente', async () => {
    iniciarSesionComo('estandar')
    const { router } = renderAppConEstado('/login', { desde: '/\\evil.example' })
    expect(await screen.findByRole('heading', { name: 'Mis hábitos' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/habitos')
  })

  it('el menú del Estándar no ofrece secciones de Administrador', async () => {
    renderApp('/habitos', { como: 'estandar' })
    const nav = await screen.findByRole('navigation', { name: 'Principal' })
    expect(within(nav).getByRole('link', { name: 'Mis hábitos' })).toBeInTheDocument()
    expect(within(nav).queryByRole('link', { name: 'Usuarios' })).not.toBeInTheDocument()
  })

  it('el Administrador no accede a hábitos (api/habits es solo Estándar)', async () => {
    renderApp('/habitos', { como: 'admin' })
    expect(await screen.findByText('No tienes acceso a esta sección')).toBeInTheDocument()
  })

  it('una sesión revocada en el servidor se cierra al recibir 401', async () => {
    const { user } = renderApp('/habitos', { como: 'estandar' })
    expect(await screen.findByText('Leer')).toBeInTheDocument()
    // Simula una revocación (p. ej. el Administrador forzó un restablecimiento).
    db.usuarios.find((u) => u.correo === 'estandar@demo.com')!.sesionesValidasDesde =
      Date.now() + 1000
    await user.click(screen.getByRole('link', { name: 'Mis permisos' }))
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
  })

  it('las piezas pendientes se muestran como "Próximamente"', async () => {
    renderApp('/reportes', { como: 'estandar' })
    expect(await screen.findByRole('heading', { name: 'Reportes' })).toBeInTheDocument()
    expect(screen.getByText('Próximamente')).toBeInTheDocument()
    expect(screen.getByText('RF-REP-01')).toBeInTheDocument()
  })
})
