import { esRol, type Rol } from './roles'

// JwtProvider emite el rol con el URI largo de ClaimTypes.Role, no como "role".
export const CLAIM_ROL = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'

export interface UsuarioSesion {
  id: string
  correo: string
  rol: Rol
  // Expiración en milisegundos desde epoch (claim exp).
  expiraEn: number
}

function decodificarBase64Url(segmento: string): string {
  const base64 = segmento.replace(/-/g, '+').replace(/_/g, '/')
  const relleno = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')
  const bytes = Uint8Array.from(atob(relleno), (c) => c.charCodeAt(0))
  return new TextDecoder().decode(bytes)
}

// Solo lee los claims para adaptar la interfaz. La firma la valida el servidor en cada
// petición (RD-06): manipular el token en el navegador no da acceso a nada.
export function leerToken(token: string): UsuarioSesion | null {
  const partes = token.split('.')
  if (partes.length !== 3) return null
  try {
    const payload = JSON.parse(decodificarBase64Url(partes[1])) as Record<string, unknown>
    const rolClaim = payload[CLAIM_ROL] ?? payload.role
    const rol = Array.isArray(rolClaim) ? rolClaim[0] : rolClaim
    if (typeof payload.sub !== 'string' || typeof payload.exp !== 'number' || !esRol(rol)) {
      return null
    }
    return {
      id: payload.sub,
      correo: typeof payload.email === 'string' ? payload.email : '',
      rol,
      expiraEn: payload.exp * 1000,
    }
  } catch {
    return null
  }
}

export function estaVencido(usuario: UsuarioSesion, ahora = Date.now()): boolean {
  return usuario.expiraEn <= ahora
}
