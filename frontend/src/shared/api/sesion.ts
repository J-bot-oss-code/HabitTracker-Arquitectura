import { tokenStorage } from '@/shared/auth/tokenStorage'
import { apiBaseUrl } from '@/shared/config/env'

// Cabecera que exige POST /api/auth/refresh: se autentica solo con la cookie, y una petición
// de otro sitio no puede añadirla (CORS la bloquea). Debe coincidir con OpcionesSesion.CabeceraCsrf.
export const CABECERA_CSRF = 'X-HabitTracker-Csrf'

type Oyente = (token: string) => void
const oyentes = new Set<Oyente>()

// El AuthProvider se entera de cada renovación para actualizar al usuario y reprogramar el temporizador.
export function alRenovarToken(oyente: Oyente): () => void {
  oyentes.add(oyente)
  return () => oyentes.delete(oyente)
}

// Dos renovaciones simultáneas con el mismo refresh token cierran la sesión (el servidor lo toma por
// robo): todas las peticiones que lo necesiten comparten UNA sola renovación en curso.
let enCurso: Promise<string | null> | null = null

// Canjea la cookie del refresh token por un token de acceso nuevo. Devuelve null si ya no hay sesión.
export function renovarToken(): Promise<string | null> {
  enCurso ??= pedirRenovacion().finally(() => {
    enCurso = null
  })
  return enCurso
}

async function pedirRenovacion(): Promise<string | null> {
  try {
    const respuesta = await fetch(`${apiBaseUrl()}/api/auth/refresh`, {
      method: 'POST',
      headers: { Accept: 'application/json', [CABECERA_CSRF]: '1' },
      credentials: 'include',
    })
    if (!respuesta.ok) return null
    const cuerpo = (await respuesta.json()) as { token?: string | null }
    if (!cuerpo.token) return null
    tokenStorage.guardar(cuerpo.token)
    oyentes.forEach((oyente) => oyente(cuerpo.token as string))
    return cuerpo.token
  } catch {
    return null
  }
}
