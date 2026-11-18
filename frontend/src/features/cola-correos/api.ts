import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/http'
import type { CorreoColaDto, EstadoCorreo, ResultadoColaDto } from './types'

const base = '/api/admin/email-queue'

export const colaCorreosApi = {
  // Hasta 200, del más reciente al más antiguo. Sin estado: todos. Un estado inválido es 400.
  listar: (estado?: EstadoCorreo) => api.get<CorreoColaDto[]>(base, { query: { estado } }),
  // Una pasada del procesador sin esperar al servicio en segundo plano.
  procesar: () => api.post<ResultadoColaDto>(`${base}/process`),
}

export const colaCorreosKeys = {
  todas: ['cola-correos'] as const,
  lista: (estado?: EstadoCorreo) => ['cola-correos', 'lista', { estado }] as const,
}

export function useColaCorreos(estado?: EstadoCorreo) {
  return useQuery({
    queryKey: colaCorreosKeys.lista(estado),
    queryFn: () => colaCorreosApi.listar(estado),
    staleTime: 0,
  })
}

export function useProcesarCola() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: colaCorreosApi.procesar,
    onSettled: () => queryClient.invalidateQueries({ queryKey: colaCorreosKeys.todas }),
  })
}
