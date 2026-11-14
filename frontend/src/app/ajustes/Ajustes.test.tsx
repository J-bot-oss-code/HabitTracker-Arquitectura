import { screen, waitFor, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { renderApp } from '@/test/render'

const secciones = () => screen.getByRole('navigation', { name: 'Secciones de configuración' })

describe('Configuración', () => {
  it.each(['/cuenta', '/ajustes'])('%s lleva a Configuración › Cuenta', async (ruta) => {
    const { router } = renderApp(ruta, { como: 'estandar' })
    expect(await screen.findByRole('heading', { level: 1, name: 'Cuenta' })).toBeInTheDocument()
    expect(router.state.location.pathname).toBe('/ajustes/cuenta')
    expect(await screen.findByText('estandar@demo.com', { selector: 'dd' })).toBeInTheDocument()
  })

  it('el Estándar ve las siete secciones y navega entre ellas', async () => {
    const { user, router } = renderApp('/ajustes/cuenta', { como: 'estandar' })
    await screen.findByRole('heading', { level: 1, name: 'Cuenta' })
    const enlaces = within(secciones())
      .getAllByRole('link')
      .map((a) => a.textContent)
    expect(enlaces).toEqual([
      'Cuenta',
      'Contraseña y seguridad',
      'Inicio de sesión',
      'Perfil y salud',
      'Solicitudes',
      'Privacidad y datos',
      'Apariencia',
    ])

    await user.click(within(secciones()).getByRole('link', { name: 'Apariencia' }))
    expect(await screen.findByRole('heading', { level: 1, name: 'Apariencia' })).toBeInTheDocument()
    await waitFor(() => expect(router.state.location.pathname).toBe('/ajustes/apariencia'))
    expect(within(secciones()).getByRole('link', { name: 'Apariencia' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('el Administrador no ve Privacidad y datos (la API se lo niega)', async () => {
    renderApp('/ajustes/cuenta', { como: 'admin' })
    await screen.findByRole('heading', { level: 1, name: 'Cuenta' })
    expect(within(secciones()).queryByRole('link', { name: 'Privacidad y datos' })).toBeNull()
    expect(screen.queryByRole('link', { name: /Privacidad y datos/ })).toBeNull()
  })

  it('el enlace de la cabecera y el menú principal llevan a Configuración', async () => {
    renderApp('/hoy', { como: 'estandar' })
    // Desde md el enlace se nombra con lo visible (correo y rol); en móvil, con un texto oculto.
    expect(
      await screen.findByRole('link', { name: /Configuración de la cuenta.*estandar@demo.com/ }),
    ).toHaveAttribute('href', '/ajustes/cuenta')
    const menu = screen.getByRole('navigation', { name: 'Principal' })
    expect(within(menu).getByRole('link', { name: 'Configuración' })).toHaveAttribute(
      'href',
      '/ajustes',
    )
  })

  it('la pestaña del navegador dice la sección y que es Configuración', async () => {
    renderApp('/ajustes/apariencia', { como: 'estandar' })
    await screen.findByRole('heading', { level: 1, name: 'Apariencia' })
    await waitFor(() =>
      expect(document.title).toBe('Apariencia · Configuración · HabitTracker Nexus'),
    )
  })

  it('en Cuenta, las tarjetas de acceso salen de la misma lista que el menú', async () => {
    renderApp('/ajustes/cuenta', { como: 'admin' })
    await screen.findByRole('heading', { level: 1, name: 'Cuenta' })
    const accesos = screen.getByRole('list', { name: 'Más ajustes' })
    const nombres = within(accesos)
      .getAllByRole('link')
      .map((a) => a.getAttribute('href'))
    expect(nombres).toEqual(['/ajustes/seguridad', '/ajustes/inicio-sesion', '/ajustes/apariencia'])
  })
})
