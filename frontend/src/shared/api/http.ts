import { tokenStorage } from '@/shared/auth/tokenStorage'
import { apiBaseUrl } from '@/shared/config/env'
import { ApiError, type ProblemDetails } from './problem'

type Metodo = 'GET' | 'POST' | 'PATCH' | 'PUT' | 'DELETE'

interface OpcionesHttp {
  body?: unknown
  query?: Record<string, string | number | boolean | undefined>
  signal?: AbortSignal
}

// AuthProvider registra aquí cómo cerrar la sesión, sin que este módulo dependa de React.
let alNoAutorizado: () => void = () => {}

export function alRecibirNoAutorizado(manejador: () => void): () => void {
  alNoAutorizado = manejador
  return () => {
    if (alNoAutorizado === manejador) alNoAutorizado = () => {}
  }
}

function construirUrl(ruta: string, query?: OpcionesHttp['query']): string {
  const url = `${apiBaseUrl()}${ruta}`
  if (!query) return url
  const params = new URLSearchParams()
  for (const [clave, valor] of Object.entries(query)) {
    if (valor !== undefined) params.set(clave, String(valor))
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

async function leerProblema(respuesta: Response): Promise<ProblemDetails | null> {
  try {
    return (await respuesta.json()) as ProblemDetails
  } catch {
    return null
  }
}

export async function http<T>(
  metodo: Metodo,
  ruta: string,
  opciones: OpcionesHttp = {},
): Promise<T> {
  const headers: Record<string, string> = { Accept: 'application/json' }
  const token = tokenStorage.leer()
  if (token) headers.Authorization = `Bearer ${token}`
  if (opciones.body !== undefined) headers['Content-Type'] = 'application/json'

  const respuesta = await fetch(construirUrl(ruta, opciones.query), {
    method: metodo,
    headers,
    body: opciones.body !== undefined ? JSON.stringify(opciones.body) : undefined,
    signal: opciones.signal,
  })

  if (!respuesta.ok) {
    // Un 401 con token significa sesión vencida o revocada (blacklist / cambio de clave).
    if (respuesta.status === 401 && token) alNoAutorizado()
    throw new ApiError(respuesta.status, await leerProblema(respuesta))
  }

  if (respuesta.status === 204) return undefined as T
  const texto = await respuesta.text()
  return (texto ? JSON.parse(texto) : undefined) as T
}

export const api = {
  get: <T>(ruta: string, opciones?: Omit<OpcionesHttp, 'body'>) => http<T>('GET', ruta, opciones),
  post: <T>(ruta: string, body?: unknown) => http<T>('POST', ruta, { body }),
  patch: <T>(ruta: string, body?: unknown) => http<T>('PATCH', ruta, { body }),
}

// Respuesta genérica { mensaje } que devuelven varios endpoints del backend.
export interface MensajeResponse {
  mensaje: string
}

export interface IdResponse {
  id: string
}
