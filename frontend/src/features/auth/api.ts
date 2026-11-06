import { useMutation, useQuery } from '@tanstack/react-query'
import { useCallback, useState } from 'react'
import { api, type MensajeResponse } from '@/shared/api/http'
import { useAuth } from '@/shared/auth/AuthContext'
import type {
  CambiarPasswordRequest,
  LoginRequest,
  MeResponse,
  RegistroRequest,
  RestablecerPasswordRequest,
  UsuarioResponseDto,
  ActivarRequest,
  GoogleLoginRequest,
  GoogleVincularRequest,
  VinculosResponse,
} from './types'

const base = '/api/auth'

export const authApi = {
  registrar: (body: RegistroRequest) => api.post<UsuarioResponseDto>(`${base}/register`, body),
  activar: (body: ActivarRequest) => api.post<MensajeResponse>(`${base}/activate`, body),
  reenviarActivacion: (correo: string) =>
    api.post<MensajeResponse>(`${base}/resend-activation`, { correo }),
  login: (body: LoginRequest) => api.post<UsuarioResponseDto>(`${base}/login`, body),
  me: () => api.get<MeResponse>(`${base}/me`),
  google: (body: GoogleLoginRequest) => api.post<UsuarioResponseDto>(`${base}/google`, body),
  vincularGoogle: (body: GoogleVincularRequest) =>
    api.post<UsuarioResponseDto>(`${base}/google/vincular`, body),
  vinculos: () => api.get<VinculosResponse>(`${base}/vinculos`),
  desvincularGoogle: () => api.delete<void>(`${base}/vinculos/google`),
  logout: () => api.post<MensajeResponse>(`${base}/logout`),
  recuperarPassword: (correo: string) =>
    api.post<MensajeResponse>(`${base}/recuperar-password`, { correo }),
  restablecerPassword: (body: RestablecerPasswordRequest) =>
    api.post<MensajeResponse>(`${base}/restablecer-password`, body),
  cambiarPassword: (body: CambiarPasswordRequest) =>
    api.post<MensajeResponse>(`${base}/cambiar-password`, body),
}

export const authKeys = {
  me: ['auth', 'me'] as const,
  vinculos: ['auth', 'vinculos'] as const,
}

export function useMe() {
  return useQuery({ queryKey: authKeys.me, queryFn: authApi.me })
}

export function useVinculos() {
  return useQuery({ queryKey: authKeys.vinculos, queryFn: authApi.vinculos })
}

export const useLogin = () => useMutation({ mutationFn: authApi.login })
export const useRegistro = () => useMutation({ mutationFn: authApi.registrar })
export const useActivar = () => useMutation({ mutationFn: authApi.activar })
export const useReenviarActivacion = () => useMutation({ mutationFn: authApi.reenviarActivacion })
export const useRecuperarPassword = () => useMutation({ mutationFn: authApi.recuperarPassword })
export const useRestablecerPassword = () => useMutation({ mutationFn: authApi.restablecerPassword })
export const useCambiarPassword = () => useMutation({ mutationFn: authApi.cambiarPassword })

// Cierre de sesión voluntario. RF-CA-18: el backend invalida el token (blacklist); localmente
// se borra siempre, aunque la llamada falle.
export function useCerrarSesion() {
  const { cerrarSesion } = useAuth()
  const [saliendo, setSaliendo] = useState(false)
  const salir = useCallback(async () => {
    setSaliendo(true)
    await authApi.logout().catch(() => undefined)
    cerrarSesion({ voluntario: true })
  }, [cerrarSesion])
  return { salir, saliendo }
}
