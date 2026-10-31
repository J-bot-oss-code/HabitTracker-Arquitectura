import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/App'
import { env } from './shared/config/env'
import './index.css'

// Con VITE_USE_MOCKS (por defecto) la API se simula en el navegador con MSW.
// El módulo de mocks solo se descarga en ese caso.
async function iniciarMocks() {
  if (!env.useMocks) return
  const { worker } = await import('./mocks/browser')
  await worker.start({ onUnhandledRequest: 'bypass', quiet: true })
}

iniciarMocks().then(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
})
