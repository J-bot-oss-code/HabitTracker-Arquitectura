import { createBrowserRouter, RouterProvider } from 'react-router'
import { Providers } from './providers'
import { rutaRaiz } from './routes'

const router = createBrowserRouter([rutaRaiz])

export function App() {
  return (
    <Providers>
      <RouterProvider router={router} />
    </Providers>
  )
}
