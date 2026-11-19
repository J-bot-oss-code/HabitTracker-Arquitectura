import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { authKeys } from '@/features/auth/api'
import { api } from '@/shared/api/http'
import { codigoDeError, esErrorHttp, esNoDisponible } from '@/shared/api/problem'
import type {
  BloqueCondiciones,
  BloqueObjetivos,
  BloqueSeguridad,
  ConsentimientosDto,
  CuestionarioSaludDto,
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

// Textos en español que otras features (planes) muestran junto a los datos del perfil y la salud.
export {
  ETIQUETA_ALERGIA,
  ETIQUETA_CONDICION,
  ETIQUETA_LUGAR,
  ETIQUETA_PERFIL_LABORAL,
  ETIQUETA_ZONA,
} from './etiquetas'

// ProfileController (HabitTracker v2, Fase 2): perfil y cuestionario de salud por bloques.
export const perfilApi = {
  obtener: () => api.get<PerfilDto>('/api/profile'),
  guardar: (body: GuardarPerfilRequest) => api.put<PerfilDto>('/api/profile', body),
  salud: () => api.get<CuestionarioSaludDto>('/api/profile/health'),
  guardarObjetivos: (body: BloqueObjetivos) =>
    api.put<CuestionarioSaludDto>('/api/profile/health/objetivos', body),
  guardarSeguridad: (body: BloqueSeguridad) =>
    api.put<CuestionarioSaludDto>('/api/profile/health/seguridad', body),
  guardarCondiciones: (body: BloqueCondiciones) =>
    api.put<CuestionarioSaludDto>('/api/profile/health/condiciones', body),
  consentimientos: () => api.get<ConsentimientosDto>('/api/privacy/consents'),
  guardarConsentimientos: (body: GuardarConsentimientosRequest) =>
    api.put<ConsentimientosDto>('/api/privacy/consents', body),
}

export const perfilKeys = {
  perfil: ['perfil'] as const,
  consentimientos: ['perfil', 'consentimientos'] as const,
  salud: ['perfil', 'salud'] as const,
}

// 409 consentimiento_salud_requerido: falta el consentimiento datosSalud para guardar un dato de salud.
export function esConsentimientoSaludRequerido(error: unknown): boolean {
  return esErrorHttp(error, 409) && codigoDeError(error) === 'consentimiento_salud_requerido'
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

export function useSalud(habilitado = true) {
  return useQuery({ queryKey: perfilKeys.salud, queryFn: perfilApi.salud, enabled: habilitado })
}

function useGuardarBloque<T>(mutationFn: (body: T) => Promise<CuestionarioSaludDto>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: (dto) => queryClient.setQueryData(perfilKeys.salud, dto),
  })
}

export const useGuardarObjetivos = () => useGuardarBloque(perfilApi.guardarObjetivos)
export const useGuardarSeguridad = () => useGuardarBloque(perfilApi.guardarSeguridad)
export const useGuardarCondiciones = () => useGuardarBloque(perfilApi.guardarCondiciones)

export function useGuardarConsentimientos() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: perfilApi.guardarConsentimientos,
    onSuccess: (consentimientos) => {
      queryClient.setQueryData(perfilKeys.consentimientos, consentimientos)
      // Con o sin consentimiento cambia si la evaluación usa los datos de salud.
      void queryClient.invalidateQueries({ queryKey: perfilKeys.salud })
      // /me trae `requiereAceptarDocumentos`: al aceptar las versiones vigentes el aviso debe desaparecer.
      return queryClient.invalidateQueries({ queryKey: authKeys.me })
    },
  })
}
