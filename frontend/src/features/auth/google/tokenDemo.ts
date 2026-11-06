// Modo demostración (VITE_USE_MOCKS): no hay Google de verdad, así que el "ID token" es un texto con la
// identidad que la API simulada acepta. Nunca llega al backend real: allí el token lo firma Google.
const PREFIJO = 'mock-google'

export interface IdentidadDemo {
  sub: string
  correo: string
  nombre: string
  correoVerificado: boolean
}

export function armarTokenDemo(
  correo: string,
  nombre = 'Persona Google',
  verificado = true,
): string {
  const limpio = correo.trim().toLowerCase()
  return [PREFIJO, `sub-${limpio}`, limpio, nombre, verificado ? '1' : '0'].join('|')
}

export function leerTokenDemo(token: unknown): IdentidadDemo | null {
  if (typeof token !== 'string') return null
  const [prefijo, sub, correo, nombre, verificado, ...resto] = token.split('|')
  if (prefijo !== PREFIJO || resto.length > 0 || !sub || !correo || verificado === undefined) {
    return null
  }
  return { sub, correo, nombre, correoVerificado: verificado === '1' }
}
