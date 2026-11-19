import '@testing-library/jest-dom/vitest'
import { cleanup, configure } from '@testing-library/react'
import { File as ArchivoNode } from 'node:buffer'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { reiniciarDb } from '@/mocks/db'
import { server } from '@/mocks/server'
import { tokenStorage } from '@/shared/auth/tokenStorage'

// Las pantallas se cargan bajo demanda (import dinámico): con toda la suite en paralelo el
// segundo por defecto de findBy se queda corto sin que haya un fallo real.
configure({ asyncUtilTimeout: 4000 })

// jsdom no implementa scrollTo (ScrollRestoration lo llama en cada navegación).
window.scrollTo = () => undefined

// En jsdom, fetch (el de Node) no reconoce el File ni el FormData de jsdom: envía un archivo como el texto
// "[object File]" y no logra leerlo en el servidor simulado. Las pruebas usan los de Node, que es lo que fetch
// entiende (el FormData de Node no está en un global porque jsdom lo tapa: se obtiene de una respuesta).
globalThis.File = ArchivoNode as unknown as typeof File
globalThis.FormData = (await new Response(new URLSearchParams()).formData())
  .constructor as typeof FormData

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
