import { tokenStorage } from '@/shared/auth/tokenStorage'
import { apiBaseUrl } from '@/shared/config/env'
import { ApiError, type ProblemDetails } from './problem'
import { renovarToken } from './sesion'

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

// Rutas de autenticación: su 401 significa "credenciales incorrectas" y no "token vencido", así que
// no se intenta renovar la sesión ni se cierra la que haya.
const RUTAS_SIN_RENOVACION = [
  '/api/auth/login',
  '/api/auth/google',
  '/api/auth/google/vincular',
  '/api/auth/refresh',
]

function hacerPeticion(metodo: Metodo, ruta: string, opciones: OpcionesHttp, token: string | null) {
  const headers: Record<string, string> = { Accept: 'application/json' }
  if (token) headers.Authorization = `Bearer ${token}`
  if (opciones.body !== undefined) headers['Content-Type'] = 'application/json'

  return fetch(construirUrl(ruta, opciones.query), {
    method: metodo,
    headers,
    body: opciones.body !== undefined ? JSON.stringify(opciones.body) : undefined,
    signal: opciones.signal,
    // La cookie HttpOnly del refresh token solo viaja a /api/auth; en el resto de la API el navegador no la envía.
    credentials: 'include',
  })
}

export async function http<T>(
  metodo: Metodo,
  ruta: string,
  opciones: OpcionesHttp = {},
): Promise<T> {
  const token = tokenStorage.leer()
  let respuesta = await hacerPeticion(metodo, ruta, opciones, token)

  // Con el token de acceso vencido (dura 15 min) se intenta UNA vez renovarlo con la cookie y repetir la
  // petición. Si ya no hay sesión (cookie vencida o revocada), entonces sí se cierra.
  if (respuesta.status === 401 && token && !RUTAS_SIN_RENOVACION.includes(ruta)) {
    const nuevo = await renovarToken()
    if (nuevo) respuesta = await hacerPeticion(metodo, ruta, opciones, nuevo)
  }

  if (!respuesta.ok) {
    // Un 401 con token que no se pudo renovar significa sesión vencida o revocada.
    if (respuesta.status === 401 && token && !RUTAS_SIN_RENOVACION.includes(ruta)) alNoAutorizado()
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
  put: <T>(ruta: string, body?: unknown) => http<T>('PUT', ruta, { body }),
  delete: <T>(ruta: string) => http<T>('DELETE', ruta),
}

// Respuesta genérica { mensaje } que devuelven varios endpoints del backend.
export interface MensajeResponse {
  mensaje: string
}

export interface IdResponse {
  id: string
}
