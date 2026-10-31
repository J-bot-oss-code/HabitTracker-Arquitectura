import { delay, HttpResponse } from 'msw'
import { CLAIM_ROL } from '@/shared/auth/jwt'
import type { Rol } from '@/shared/auth/roles'
import { db, type UsuarioMock } from './db'

export const MINUTOS_EXPIRACION = 60

// Mismas respuestas que GlobalExceptionMiddleware: títulos y detalles genéricos (RD-08).
export function problema(status: number, title: string, detail?: string) {
  return HttpResponse.json(
    {
      type: `https://httpstatuses.io/${status}`,
      title,
      status,
      detail,
      instance: crypto.randomUUID(),
    },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  )
}

export const respuestas = {
  invalida: () => problema(400, 'Solicitud inválida.', 'Los datos enviados no son válidos.'),
  operacion: () =>
    problema(
      400,
      'La operación no pudo completarse.',
      'La operación no está permitida en el estado actual.',
    ),
  // JwtBearer responde 401 sin cuerpo.
  noAutenticado: () => new HttpResponse(null, { status: 401 }),
  noAutorizado: () => problema(401, 'No autorizado.', 'Credenciales incorrectas.'),
  prohibido: () => new HttpResponse(null, { status: 403 }),
  noEncontrado: (detail: string) => problema(404, 'No encontrado.', detail),
  mensaje: (mensaje: string) => HttpResponse.json({ mensaje }),
}

// Latencia realista en el navegador; inmediata en las pruebas.
export const latencia = () => (import.meta.env.MODE === 'test' ? Promise.resolve() : delay(250))

export async function leerJson<T>(request: Request): Promise<Partial<T> | null> {
  try {
    const cuerpo: unknown = await request.json()
    return cuerpo && typeof cuerpo === 'object' ? (cuerpo as Partial<T>) : null
  } catch {
    return null
  }
}

function base64Url(texto: string): string {
  const bytes = new TextEncoder().encode(texto)
  let binario = ''
  bytes.forEach((b) => (binario += String.fromCharCode(b)))
  return btoa(binario).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

// Token con la misma forma de claims que JwtProvider. La firma es ficticia: solo la API
// simulada lo acepta.
export function emitirToken(usuario: UsuarioMock): string {
  const ahora = Date.now()
  const header = base64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))
  const payload = base64Url(
    JSON.stringify({
      sub: usuario.id,
      email: usuario.correo,
      [CLAIM_ROL]: usuario.rol,
      jti: crypto.randomUUID(),
      iat: Math.floor(ahora / 1000),
      session_issued_at_ticks: ahora,
      exp: Math.floor(ahora / 1000) + MINUTOS_EXPIRACION * 60,
      iss: 'HabitTrackerNexus',
      aud: 'HabitTrackerNexusApi',
    }),
  )
  return `${header}.${payload}.firma-simulada`
}

interface ClaimsMock {
  sub: string
  jti: string
  exp: number
  session_issued_at_ticks: number
}

function leerClaims(token: string): ClaimsMock | null {
  try {
    const segmento = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')
    const binario = atob(segmento.padEnd(Math.ceil(segmento.length / 4) * 4, '='))
    return JSON.parse(new TextDecoder().decode(Uint8Array.from(binario, (c) => c.charCodeAt(0))))
  } catch {
    return null
  }
}

export interface Sesion {
  usuario: UsuarioMock
  jti: string
}

// Equivalente a JwtBearer + TokenBlacklistMiddleware + [Authorize(Roles = ...)].
export function autenticar(
  request: Request,
  roles?: Rol[],
): { sesion: Sesion; error?: never } | { sesion?: never; error: Response } {
  const cabecera = request.headers.get('Authorization') ?? ''
  const token = cabecera.startsWith('Bearer ') ? cabecera.slice(7) : ''
  const claims = token ? leerClaims(token) : null
  const usuario = claims && db.usuarios.find((u) => u.id === claims.sub)

  const valido =
    claims &&
    usuario &&
    usuario.activo &&
    claims.exp * 1000 > Date.now() &&
    !db.tokensRevocados.has(claims.jti) &&
    claims.session_issued_at_ticks >= usuario.sesionesValidasDesde

  if (!valido) return { error: respuestas.noAutenticado() }
  if (roles && !roles.includes(usuario.rol)) return { error: respuestas.prohibido() }
  return { sesion: { usuario, jti: claims.jti } }
}

export const esTexto = (v: unknown, min: number, max: number): v is string =>
  typeof v === 'string' && v.trim().length >= min && v.trim().length <= max

export const esCorreo = (v: unknown): v is string =>
  esTexto(v, 3, 150) && /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(v.trim())

// UsuarioService.ValidarFormatoClave.
export const cumplePolitica = (v: unknown): v is string =>
  typeof v === 'string' && v.length >= 8 && v.length <= 1024 && /\p{L}/u.test(v) && /\d/.test(v)

// Las fechas de HabitTracker se leen de SQL sin Kind UTC y salen sin "Z" (deuda §6.4).
export const comoFechaSql = (iso: string) => iso.replace(/Z$/, '')
