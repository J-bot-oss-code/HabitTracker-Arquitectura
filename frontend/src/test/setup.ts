import '@testing-library/jest-dom/vitest'
import { cleanup, configure } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { reiniciarDb } from '@/mocks/db'
import { server } from '@/mocks/server'
import { tokenStorage } from '@/shared/auth/tokenStorage'

// Las pantallas se cargan bajo demanda (import dinámico): con toda la suite en paralelo el
// segundo por defecto de findBy se queda corto sin que haya un fallo real.
configure({ asyncUtilTimeout: 4000 })

// jsdom no implementa scrollTo (ScrollRestoration lo llama en cada navegación).
window.scrollTo = () => undefined

// Toda petición sin handler falla la prueba: así se detecta cualquier ruta fuera del contrato.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))

afterEach(() => {
  cleanup()
  server.resetHandlers()
  reiniciarDb()
  tokenStorage.borrar()
  // Avisos descartados y otras preferencias de la pestaña no pasan de una prueba a otra.
  sessionStorage.clear()
})

afterAll(() => server.close())
