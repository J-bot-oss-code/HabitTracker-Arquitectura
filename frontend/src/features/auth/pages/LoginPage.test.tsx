import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { db, PASSWORD_DEMO } from '@/mocks/db'
import { renderApp } from '@/test/render'

describe('LoginPage', () => {
  it('un usuario Estándar entra y llega a su panel Hoy', async () => {
    const { user, router } = renderApp('/login')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), PASSWORD_DEMO)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(
      await screen.findByRole('heading', {
        level: 1,
        name: /^(Buenos días|Buenas tardes|Buenas noches)/,
      }),
    ).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/hoy')
    expect(await screen.findByRole('link', { name: 'Leer' })).toBeInTheDocument()
  })

  it('un Administrador llega a su resumen', async () => {
    const { user, router } = renderApp('/login')
    await user.type(screen.getByLabelText('Correo'), 'admin@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), PASSWORD_DEMO)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByRole('heading', { level: 1, name: 'Resumen' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/admin')
  })

  it('con credenciales incorrectas muestra un mensaje que no revela qué falló', async () => {
    const { user } = renderApp('/login')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'incorrecta1')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Correo o contraseña incorrectos.')).toBeInTheDocument()
  })

  it('una cuenta desactivada por un administrador recibe ese aviso', async () => {
    const otro = db.usuarios.find((u) => u.correo === 'otro@demo.com')
    if (otro) otro.activo = false
    const { user } = renderApp('/login')
    await user.type(screen.getByLabelText('Correo'), 'otro@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), PASSWORD_DEMO)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText(/desactivada por un administrador/)).toBeInTheDocument()
  })

  it('tras 5 intentos fallidos avisa del bloqueo temporal', async () => {
    const { user } = renderApp('/login')
    for (let i = 0; i < 5; i++) {
      await user.clear(screen.getByLabelText('Correo'))
      await user.type(screen.getByLabelText('Correo'), 'otro@demo.com')
      await user.clear(screen.getByLabelText('Contraseña'))
      await user.type(screen.getByLabelText('Contraseña'), 'incorrecta-12345')
      await user.click(screen.getByRole('button', { name: 'Entrar' }))
      await screen.findByRole('alert')
    }

    expect(await screen.findByText(/bloqueada 15 minutos/)).toBeInTheDocument()
  })

  it('una cuenta sin activar recibe el aviso de activación', async () => {
    const { user } = renderApp('/login')
    await user.type(screen.getByLabelText('Correo'), 'inactiva@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), PASSWORD_DEMO)
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText(/todavía no está activada/)).toBeInTheDocument()
  })

  it('valida el formulario antes de llamar a la API', async () => {
    const { user } = renderApp('/login')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByText('El correo es obligatorio.')).toBeInTheDocument()
    expect(screen.getByText('La contraseña es obligatoria.')).toBeInTheDocument()
  })
})
