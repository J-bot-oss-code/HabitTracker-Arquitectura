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

// GET /api/auth/sesiones: una sesión abierta en un dispositivo. `dispositivo` es el User-Agent que mandó ese navegador
// (texto del cliente: se muestra como texto, nunca como HTML). `actual` marca la del dispositivo que consulta.
export interface SesionActivaDto {
  id: string
  dispositivo: string
  creadaEn: string
  ultimoUsoEn: string
  actual: boolean
}

// POST /api/auth/google: el ID token que entrega Google Identity Services. Para una cuenta nueva exige los
// consentimientos (400 consentimiento_requerido si faltan); si el correo ya tiene una cuenta con contraseña
// responde 409 vinculacion_requerida y hay que usar /google/vincular.
export interface GoogleLoginRequest {
  idToken: string
  consentimientos?: ConsentimientosRegistro
}

export interface GoogleVincularRequest {
  idToken: string
  password: string
}

export interface VinculoDto {
  proveedor: 'google'
  vinculadoEn: string
}

// GET /api/auth/vinculos: formas de entrar de la cuenta. Sin contraseña no se puede desvincular Google.
export interface VinculosResponse {
  tienePassword: boolean
  vinculos: VinculoDto[]
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
