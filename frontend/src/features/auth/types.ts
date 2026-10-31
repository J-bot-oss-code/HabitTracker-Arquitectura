// Contrato de AuthController (/api/auth). Ver Host/WebApi/Controllers/AuthController.cs.
import type { Rol } from '@/shared/auth/roles'

export interface UsuarioResponseDto {
  id: string
  nombre: string
  correo: string
  rol: Rol
  activo: boolean
  token: string | null
}

export interface MeResponse {
  id: string
  correo: string
  rol: Rol
}

export interface RegistroRequest {
  nombre: string
  correo: string
  password: string
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
