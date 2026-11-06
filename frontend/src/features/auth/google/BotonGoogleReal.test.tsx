import { render, screen, waitFor } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { BotonGoogleReal } from './BotonGoogle'
import { reiniciarCargaGoogle, URL_GIS } from './gis'

// El botón real lo dibuja Google; aquí se sustituye por un doble para comprobar cómo se configura.
function instalarGoogleFalso() {
  const id = { initialize: vi.fn(), renderButton: vi.fn() }
  window.google = { accounts: { id } }
  return id
}

afterEach(() => {
  delete window.google
  reiniciarCargaGoogle()
  document.head.querySelectorAll(`script[src="${URL_GIS}"]`).forEach((s) => s.remove())
})

describe('BotonGoogleReal', () => {
  it('inicializa Google con el ID de cliente, en ventana emergente, y dibuja su botón en español', async () => {
    const google = instalarGoogleFalso()

    render(
      <BotonGoogleReal
        clienteId="123.apps.googleusercontent.com"
        texto="signup_with"
        alCredencial={vi.fn()}
      />,
    )

    await waitFor(() => expect(google.renderButton).toHaveBeenCalledTimes(1))
    expect(google.initialize).toHaveBeenCalledWith(
      expect.objectContaining({ client_id: '123.apps.googleusercontent.com', ux_mode: 'popup' }),
    )
    const [elemento, opciones] = google.renderButton.mock.calls[0]
    expect(elemento).toBe(screen.getByTestId('boton-google'))
    expect(opciones).toMatchObject({
      type: 'standard',
      text: 'signup_with',
      locale: 'es',
      size: 'large',
    })
    expect(opciones.width).toBeGreaterThanOrEqual(200)
    expect(opciones.width).toBeLessThanOrEqual(340)
  })

  it('entrega a quien lo usa el ID token que devuelve Google (sin interpretarlo)', async () => {
    const google = instalarGoogleFalso()
    const alCredencial = vi.fn()
    render(
      <BotonGoogleReal
        clienteId="x.apps.googleusercontent.com"
        texto="signin_with"
        alCredencial={alCredencial}
      />,
    )
    await waitFor(() => expect(google.initialize).toHaveBeenCalled())

    google.initialize.mock.calls[0][0].callback({ credential: 'eyJ.id.token' })

    expect(alCredencial).toHaveBeenCalledWith('eyJ.id.token')
  })

  it('usa siempre la última función recibida, sin volver a inicializar Google', async () => {
    const google = instalarGoogleFalso()
    const primera = vi.fn()
    const segunda = vi.fn()
    const { rerender } = render(
      <BotonGoogleReal
        clienteId="x.apps.googleusercontent.com"
        texto="signin_with"
        alCredencial={primera}
      />,
    )
    await waitFor(() => expect(google.initialize).toHaveBeenCalledTimes(1))

    rerender(
      <BotonGoogleReal
        clienteId="x.apps.googleusercontent.com"
        texto="signin_with"
        alCredencial={segunda}
      />,
    )
    google.initialize.mock.calls[0][0].callback({ credential: 'token' })

    expect(google.initialize).toHaveBeenCalledTimes(1)
    expect(primera).not.toHaveBeenCalled()
    expect(segunda).toHaveBeenCalledWith('token')
  })

  it('si Google no se puede cargar avisa y deja usar el correo y la contraseña', async () => {
    render(
      <BotonGoogleReal
        clienteId="x.apps.googleusercontent.com"
        texto="signin_with"
        alCredencial={vi.fn()}
      />,
    )

    document.head.querySelector(`script[src="${URL_GIS}"]`)?.dispatchEvent(new Event('error'))

    expect(await screen.findByText(/No se pudo cargar el inicio con Google/)).toBeInTheDocument()
    expect(screen.getByText(/usa tu correo y contraseña/)).toBeInTheDocument()
  })

  it('mientras carga muestra un estado y no dibuja nada hasta tener Google', () => {
    render(
      <BotonGoogleReal
        clienteId="x.apps.googleusercontent.com"
        texto="signin_with"
        alCredencial={vi.fn()}
      />,
    )

    expect(screen.getByText('Cargando Google…')).toBeInTheDocument()
  })

  it('al desmontarse antes de que Google cargue no dibuja el botón', async () => {
    const { unmount } = render(
      <BotonGoogleReal
        clienteId="x.apps.googleusercontent.com"
        texto="signin_with"
        alCredencial={vi.fn()}
      />,
    )
    unmount()
    const google = instalarGoogleFalso()
    document.head.querySelector(`script[src="${URL_GIS}"]`)?.dispatchEvent(new Event('load'))
    await new Promise((resolver) => setTimeout(resolver, 20))

    expect(google.renderButton).not.toHaveBeenCalled()
  })
})
