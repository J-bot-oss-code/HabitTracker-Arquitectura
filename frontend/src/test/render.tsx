import { QueryClient } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { Providers } from '@/app/providers'
import { rutas } from '@/app/routes'
import { db } from '@/mocks/db'
import { emitirToken } from '@/mocks/http'
import { tokenStorage } from '@/shared/auth/tokenStorage'

export const CORREOS = {
  estandar: 'estandar@demo.com',
  admin: 'admin@demo.com',
  otro: 'otro@demo.com',
} as const

// Abre sesión sin pasar por la pantalla de login (token emitido por la API simulada).
export function iniciarSesionComo(quien: keyof typeof CORREOS): string {
  const usuario = db.usuarios.find((u) => u.correo === CORREOS[quien])
  if (!usuario) throw new Error(`Usuario demo inexistente: ${quien}`)
  const token = emitirToken(usuario)
  tokenStorage.guardar(token)
  return token
}

// Renderiza la aplicación completa en la ruta indicada, contra la API simulada.
export function renderApp(ruta: string, opciones: { como?: keyof typeof CORREOS } = {}) {
  if (opciones.como) iniciarSesionComo(opciones.como)
  // Como texto para conservar la query (?token=...).
  return montar(ruta)
}

// Igual que renderApp, pero con un location.state inicial (p. ej. { desde }).
export function renderAppConEstado(ruta: string, state: unknown) {
  return montar({ pathname: ruta, state })
}

function montar(entrada: string | { pathname: string; state: unknown }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })
  const router = createMemoryRouter([{ path: '/', children: rutas }], {
    initialEntries: [entrada],
  })
  const user = userEvent.setup()
  render(
    <Providers queryClient={queryClient}>
      <RouterProvider router={router} />
    </Providers>,
  )
  return { user, router }
}
