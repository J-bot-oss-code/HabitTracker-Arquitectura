import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './app/App'
import { env } from './shared/config/env'
import './index.css'

// Con VITE_USE_MOCKS (por defecto en desarrollo) la API se simula dentro de la página con los
// handlers de MSW, sin Service Worker. El módulo de mocks solo se descarga en ese caso.
async function iniciarMocks() {
  // Versiones anteriores registraban el Service Worker de MSW; se elimina si sigue instalado.
  navigator.serviceWorker
    ?.getRegistrations()
    .then((registros) =>
      registros
        .filter((r) => r.active?.scriptURL.endsWith('/mockServiceWorker.js'))
        .forEach((r) => r.unregister()),
    )
    .catch(() => undefined)

  if (!env.useMocks) return
  const { instalarFetchSimulado } = await import('./mocks/fetchSimulado')
  instalarFetchSimulado()
}

iniciarMocks().then(() => {
  createRoot(document.getElementById('root')!).render(
    <StrictMode>
      <App />
    </StrictMode>,
  )
})
