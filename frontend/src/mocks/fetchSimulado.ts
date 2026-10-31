import { getResponse, type RequestHandler } from 'msw'
import { handlers as handlersPorDefecto } from './handlers'

// API simulada para el modo demo, resuelta dentro de la página: envuelve fetch y responde
// /api/* con los mismos handlers que usan las pruebas. No depende de un Service Worker, así
// que funciona en modo privado, tras una recarga forzada y en navegadores que los bloquean.
export function crearFetchSimulado(
  fetchReal: typeof fetch,
  origen: string,
  handlers: RequestHandler[] = handlersPorDefecto,
): typeof fetch {
  return async (entrada, init) => {
    const request = new Request(entrada, init)
    const url = new URL(request.url)
    if (url.origin !== origen || !url.pathname.startsWith('/api/')) {
      // Se reenvía el Request ya construido: si la entrada traía cuerpo, ya se consumió.
      return fetchReal(request)
    }
    const respuesta = await getResponse(handlers, request, { baseUrl: origen })
    // Una ruta /api sin handler es una ruta que el backend tampoco tiene.
    return respuesta ?? new Response(null, { status: 404 })
  }
}

export function instalarFetchSimulado() {
  window.fetch = crearFetchSimulado(window.fetch.bind(window), window.location.origin)
}
