// Contrato de AuthController (/api/auth). Ver Host/WebApi/Controllers/AuthController.cs.
import type { Rol } from '@/shared/auth/roles'

export interface UsuarioResponseDto {
  id: string
  nombre: string
  correo: string
  rol: Rol
  activo: boolean
  token: string | null
  // true si la cuenta nunca aceptó las versiones vigentes de los documentos legales (o cambiaron).
  requiereAceptarDocumentos: boolean
  // Fin del periodo de gracia de una baja pedida (UTC); null si no hay baja programada.
  bajaProgramadaPara: string | null
}

export interface MeResponse {
  id: string
  nombre: string | null
  correo: string
  rol: Rol
  requiereAceptarDocumentos: boolean
}

// Lo que se acepta al registrarse (versiones vigentes de los documentos). Sin ello el backend no crea la cuenta.
export interface ConsentimientosRegistro {
  terminosVersion: string
  privacidadVersion: string
  marketing: boolean
}

export interface RegistroRequest {
  nombre: string
  correo: string
  password: string
  consentimientos: ConsentimientosRegistro
}

// POST /api/auth/activate: el código de 6 dígitos no es único entre usuarios, siempre va con el correo.
export interface ActivarRequest {
  correo: string
  codigo: string
}

// Ojo: el login usa "email"; el resto de endpoints usa "correo".
export interface LoginRequest {
  email: string
  password: string
}

export interface RestablecerPasswordRequest {
  correo: string
  codigo: string
  nuevaPassword: string
}

export interface CambiarPasswordRequest {
  actual: string
  nueva: string
}
