import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { PASSWORD_DEMO } from '@/mocks/db'
import { renderApp } from '@/test/render'

describe('LoginPage', () => {
  it('un usuario Estándar entra y llega a sus hábitos', async () => {
    const { user, router } = renderApp('/login')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), PASSWORD_DEMO)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('heading', { name: 'Mis hábitos' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/habitos')
    expect(await screen.findByText('Leer')).toBeInTheDocument()
  })

  it('un Administrador llega a la bandeja de solicitudes', async () => {
    const { user, router } = renderApp('/login')
    await user.type(screen.getByLabelText('Correo'), 'admin@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), PASSWORD_DEMO)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(
      await screen.findByRole('heading', { name: 'Solicitudes de permiso' }),
    ).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/admin/permisos')
  })

  it('con credenciales incorrectas muestra un mensaje que no revela qué falló', async () => {
    const { user } = renderApp('/login')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'incorrecta1')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Correo o contraseña incorrectos.')).toBeInTheDocument()
  })

  it('una cuenta sin activar recibe el aviso de activación', async () => {
    const { user } = renderApp('/login')
    await user.type(screen.getByLabelText('Correo'), 'inactiva@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), PASSWORD_DEMO)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText(/Verifica que tu cuenta esté activada/)).toBeInTheDocument()
  })

  it('valida el formulario antes de llamar a la API', async () => {
    const { user } = renderApp('/login')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByText('El correo es obligatorio.')).toBeInTheDocument()
    expect(screen.getByText('La contraseña es obligatoria.')).toBeInTheDocument()
  })
})
