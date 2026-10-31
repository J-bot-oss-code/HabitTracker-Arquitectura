import { screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { CODIGO_RECUPERACION_DEMO, TOKEN_ACTIVACION_DEMO } from '@/mocks/db'
import { renderApp } from '@/test/render'

describe('flujos de cuenta', () => {
  it('registro → activación → login', async () => {
    const { user } = renderApp('/registro')
    await user.type(screen.getByLabelText('Nombre completo'), 'Nueva Persona')
    await user.type(screen.getByLabelText('Correo'), 'nueva@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'Clave1234')
    await user.type(screen.getByLabelText('Confirmar contraseña'), 'Clave1234')
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(await screen.findByText('Cuenta creada')).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Tengo un código de activación' }))
    await user.type(screen.getByLabelText('Código de activación'), TOKEN_ACTIVACION_DEMO)
    await user.click(screen.getByRole('button', { name: 'Activar' }))
    expect(await screen.findByText(/Tu cuenta está activa/)).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Ir a iniciar sesión' }))
    await user.type(screen.getByLabelText('Correo'), 'nueva@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'Clave1234')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByRole('heading', { name: 'Mis hábitos' })).toBeInTheDocument()
  })

  it('rechaza registrar un correo existente con un mensaje genérico', async () => {
    const { user } = renderApp('/registro')
    await user.type(screen.getByLabelText('Nombre completo'), 'Duplicado')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'Clave1234')
    await user.type(screen.getByLabelText('Confirmar contraseña'), 'Clave1234')
    await user.click(screen.getByRole('button', { name: 'Crear cuenta' }))
    expect(await screen.findByRole('alert')).toHaveTextContent(/no está permitida/)
  })

  it('activa automáticamente con el token del enlace', async () => {
    renderApp(`/activar?token=${TOKEN_ACTIVACION_DEMO}`)
    expect(await screen.findByText(/Tu cuenta está activa/)).toBeInTheDocument()
  })

  it('recuperar → restablecer → login con la nueva contraseña', async () => {
    const { user } = renderApp('/recuperar')
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.click(screen.getByRole('button', { name: 'Enviar código' }))
    expect(await screen.findByText(/Si el correo está registrado/)).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Ya tengo el código' }))
    expect(screen.getByLabelText('Correo')).toHaveValue('estandar@demo.com')
    await user.type(screen.getByLabelText('Código'), CODIGO_RECUPERACION_DEMO)
    await user.type(screen.getByLabelText('Nueva contraseña'), 'NuevaClave9')
    await user.type(screen.getByLabelText('Confirmar contraseña'), 'NuevaClave9')
    await user.click(screen.getByRole('button', { name: 'Restablecer' }))

    expect(await screen.findByText(/Contraseña restablecida/)).toBeInTheDocument()
    await user.type(screen.getByLabelText('Correo'), 'estandar@demo.com')
    await user.type(screen.getByLabelText('Contraseña'), 'NuevaClave9')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    expect(await screen.findByRole('heading', { name: 'Mis hábitos' })).toBeInTheDocument()
  })

  it('cambiar la contraseña cierra la sesión (RF-CA-12)', async () => {
    const { user, router } = renderApp('/cuenta', { como: 'estandar' })
    expect(await screen.findByText('estandar@demo.com', { selector: 'dd' })).toBeInTheDocument()
    await user.type(screen.getByLabelText('Contraseña actual'), 'Demo1234')
    await user.type(screen.getByLabelText('Nueva contraseña'), 'OtraClave7')
    await user.type(screen.getByLabelText('Confirmar nueva contraseña'), 'OtraClave7')
    await user.click(screen.getByRole('button', { name: 'Actualizar contraseña' }))

    expect(await screen.findByText(/Contraseña actualizada/)).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })

  it('cerrar sesión vuelve al login y revoca el token', async () => {
    const { user, router } = renderApp('/habitos', { como: 'estandar' })
    await user.click(await screen.findByRole('button', { name: 'Cerrar sesión' }))
    expect(await screen.findByRole('heading', { name: 'Iniciar sesión' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/login')
  })
})
