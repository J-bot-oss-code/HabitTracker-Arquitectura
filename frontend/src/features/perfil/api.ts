import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/http'
import { esErrorHttp, esNoDisponible } from '@/shared/api/problem'
import type {
  ConsentimientosDto,
  GuardarConsentimientosRequest,
  GuardarPerfilRequest,
  PerfilDto,
} from './types'

// El registro (feature auth) pide los mismos consentimientos que el onboarding.
export { consentimientoSchema, type ConsentimientoForm } from './schemas'

export {
  aceptoVersionesVigentes,
  borrarRegistroPendiente,
  guardarRegistroPendiente,
  leerRegistroPendiente,
  purgarRegistroPendienteCaducado,
  type RegistroPendiente,
} from './registroPendiente'

// Contratos propuestos (sin backend todavía): ver types.ts.
export const perfilApi = {
  obtener: () => api.get<PerfilDto>('/api/profile'),
  guardar: (body: GuardarPerfilRequest) => api.put<PerfilDto>('/api/profile', body),
  consentimientos: () => api.get<ConsentimientosDto>('/api/privacy/consents'),
  guardarConsentimientos: (body: GuardarConsentimientosRequest) =>
    api.put<ConsentimientosDto>('/api/privacy/consents', body),
}

export const perfilKeys = {
  perfil: ['perfil'] as const,
  consentimientos: ['perfil', 'consentimientos'] as const,
}

// 404 con ProblemDetails: la cuenta aún no completó el onboarding. Un 404 sin cuerpo es otra
// cosa: el endpoint no existe (backend real sin esta pieza), ver esNoDisponible.
export function esPerfilInexistente(error: unknown): boolean {
  return esErrorHttp(error, 404) && !esNoDisponible(error)
}

export function usePerfil(habilitado = true) {
  return useQuery({
    queryKey: perfilKeys.perfil,
    queryFn: perfilApi.obtener,
    enabled: habilitado,
    // El reintento por defecto (solo 5xx y red) deja pasar al instante "sin perfil" (404) y
    // "pieza no disponible".
    staleTime: 60_000,
    // Una consulta fallida (404 "sin perfil" o pieza no disponible) no se repite al montar otro
    // observador: en v5 volvería a "pending" y el guard RequierePerfil desmontaría la página
    // que acaba de montar, en un ciclo sin fin.
    retryOnMount: false,
  })
}

export function useConsentimientos() {
  return useQuery({
    queryKey: perfilKeys.consentimientos,
    queryFn: perfilApi.consentimientos,
    // Igual que el perfil: un 404 no se repite al montar otro observador.
    retryOnMount: false,
  })
}

export function useGuardarPerfil() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: perfilApi.guardar,
    onSuccess: (perfil) => queryClient.setQueryData(perfilKeys.perfil, perfil),
  })
}

export function useGuardarConsentimientos() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: perfilApi.guardarConsentimientos,
    onSuccess: (consentimientos) =>
      queryClient.setQueryData(perfilKeys.consentimientos, consentimientos),
  })
}
