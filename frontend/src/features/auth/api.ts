import { useMutation, useQuery } from '@tanstack/react-query'
import { api, type MensajeResponse } from '@/shared/api/http'
import type {
  CambiarPasswordRequest,
  LoginRequest,
  MeResponse,
  RegistroRequest,
  RestablecerPasswordRequest,
  UsuarioResponseDto,
} from './types'

const base = '/api/auth'

export const authApi = {
  registrar: (body: RegistroRequest) => api.post<UsuarioResponseDto>(`${base}/register`, body),
  activar: (token: string) => api.post<MensajeResponse>(`${base}/activate`, { token }),
  reenviarActivacion: (correo: string) =>
    api.post<MensajeResponse>(`${base}/resend-activation`, { correo }),
  login: (body: LoginRequest) => api.post<UsuarioResponseDto>(`${base}/login`, body),
  me: () => api.get<MeResponse>(`${base}/me`),
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
}

export function useMe() {
  return useQuery({ queryKey: authKeys.me, queryFn: authApi.me })
}

export const useLogin = () => useMutation({ mutationFn: authApi.login })
export const useRegistro = () => useMutation({ mutationFn: authApi.registrar })
export const useActivar = () => useMutation({ mutationFn: authApi.activar })
export const useReenviarActivacion = () => useMutation({ mutationFn: authApi.reenviarActivacion })
export const useRecuperarPassword = () => useMutation({ mutationFn: authApi.recuperarPassword })
export const useRestablecerPassword = () => useMutation({ mutationFn: authApi.restablecerPassword })
export const useCambiarPassword = () => useMutation({ mutationFn: authApi.cambiarPassword })
