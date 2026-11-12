import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { perfilKeys } from '@/features/perfil/api'
import { api } from '@/shared/api/http'
import type { MedicionDto, PesosDto, RegistrarMedicionRequest, RegistrarPesoRequest } from './types'

// Contratos propuestos (sin backend todavía): ver types.ts.
export const progresoApi = {
  pesos: () => api.get<PesosDto>('/api/progress/weights'),
  registrarPeso: (body: RegistrarPesoRequest) => api.post<PesosDto>('/api/progress/weights', body),
  mediciones: () => api.get<MedicionDto[]>('/api/progress/measurements'),
  registrarMedicion: (body: RegistrarMedicionRequest) =>
    api.post<MedicionDto>('/api/progress/measurements', body),
}

export const progresoKeys = {
  pesos: ['progreso', 'pesos'] as const,
  mediciones: ['progreso', 'mediciones'] as const,
}

export const usePesos = () => useQuery({ queryKey: progresoKeys.pesos, queryFn: progresoApi.pesos })

export const useMediciones = () =>
  useQuery({ queryKey: progresoKeys.mediciones, queryFn: progresoApi.mediciones })

export function useRegistrarPeso() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: progresoApi.registrarPeso,
    onSuccess: (pesos) => {
      queryClient.setQueryData(progresoKeys.pesos, pesos)
      // El último peso pasa al perfil en el servidor: el perfil en caché ya no es el vigente.
      void queryClient.invalidateQueries({ queryKey: perfilKeys.perfil })
    },
  })
}

export function useRegistrarMedicion() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: progresoApi.registrarMedicion,
    onSettled: () => queryClient.invalidateQueries({ queryKey: progresoKeys.mediciones }),
  })
}
