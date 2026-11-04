import { render, screen } from '@testing-library/react'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { ErrorRuta } from './ErrorRuta'

function Rota(): never {
  throw new Error('Fallo interno en /srv/ruta/secreta.ts:42')
}

describe('límite de errores de las rutas (RD-08)', () => {
  it('muestra un mensaje genérico, sin el mensaje ni la pila del error', async () => {
    // React registra el error capturado en la consola; aquí se silencia.
    const consola = vi.spyOn(console, 'error').mockImplementation(() => undefined)
    const router = createMemoryRouter([
      { path: '/', errorElement: <ErrorRuta />, element: <Rota /> },
    ])
    render(<RouterProvider router={router} />)

    expect(
      await screen.findByRole('heading', { level: 1, name: 'Algo salió mal' }),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Recargar' })).toBeInTheDocument()
    expect(screen.queryByText(/secreta|Fallo interno/)).not.toBeInTheDocument()
    consola.mockRestore()
  })
})
