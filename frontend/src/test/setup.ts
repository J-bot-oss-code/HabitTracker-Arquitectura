import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterAll, afterEach, beforeAll } from 'vitest'
import { reiniciarDb } from '@/mocks/db'
import { server } from '@/mocks/server'
import { tokenStorage } from '@/shared/auth/tokenStorage'

// Toda petición sin handler falla la prueba: así se detecta cualquier ruta fuera del contrato.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))

afterEach(() => {
  cleanup()
  server.resetHandlers()
  reiniciarDb()
  tokenStorage.borrar()
})

afterAll(() => server.close())
