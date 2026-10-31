import { createBrowserRouter, RouterProvider } from 'react-router'
import { Providers } from './providers'
import { rutas } from './routes'

const router = createBrowserRouter([{ path: '/', children: rutas }])

export function App() {
  return (
    <Providers>
      <RouterProvider router={router} />
    </Providers>
  )
}
