/// <reference types="vitest/config" />
import { fileURLToPath, URL } from 'node:url'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: { '@': fileURLToPath(new URL('./src', import.meta.url)) },
  },
  server: { port: 5173 },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    restoreMocks: true,
    // Cada archivo crea un jsdom y carga rutas perezosas: con todos los núcleos a la vez las
    // pruebas se pisan y vencen sus esperas sin un fallo real.
    maxWorkers: 4,
    // Las pruebas corren siempre contra la API simulada y sin Google, pase lo que pase en tu .env.local
    // (con VITE_USE_MOCKS=false para probar el backend real, la suite entera llamaría a la red y fallaría).
    env: { VITE_USE_MOCKS: 'true', VITE_GOOGLE_CLIENT_ID: '' },
  },
})
