import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type IdResponse } from '@/shared/api/http'
import type {
  CrearHabitoRequest,
  EstadoHabito,
  HabitoDto,
  ReconfigurarHabitoRequest,
  RegistrarAvanceRequest,
} from './types'

const base = '/api/habits'

export const habitsApi = {
  listar: () => api.get<HabitoDto[]>(base),
  crear: (body: CrearHabitoRequest) => api.post<IdResponse>(base, body),
  transicionar: (id: string, nuevoEstado: EstadoHabito) =>
    api.post<void>(`${base}/${id}/transition`, { nuevoEstado }),
  registrarAvance: (id: string, body: RegistrarAvanceRequest) =>
    api.post<void>(`${base}/${id}/progress`, body),
  reconfigurar: (id: string, body: ReconfigurarHabitoRequest) =>
    api.post<void>(`${base}/${id}/advanced-reconfig`, body),
}

export const habitKeys = {
  todos: ['habits'] as const,
}

export function useHabitos() {
  return useQuery({ queryKey: habitKeys.todos, queryFn: habitsApi.listar })
}

// El backend no expone GET /api/habits/{id}: el detalle sale del listado del usuario.
export function useHabito(id: string) {
  return useQuery({
    queryKey: habitKeys.todos,
    queryFn: habitsApi.listar,
    select: (habitos) => habitos.find((h) => h.id === id) ?? null,
  })
}

function useMutacionHabitos<TVariables, TData>(mutationFn: (v: TVariables) => Promise<TData>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSettled: () => queryClient.invalidateQueries({ queryKey: habitKeys.todos }),
  })
}

export const useCrearHabito = () => useMutacionHabitos(habitsApi.crear)

export const useTransicionHabito = (id: string) =>
  useMutacionHabitos((nuevoEstado: EstadoHabito) => habitsApi.transicionar(id, nuevoEstado))

export const useRegistrarAvance = (id: string) =>
  useMutacionHabitos((body: RegistrarAvanceRequest) => habitsApi.registrarAvance(id, body))

export const useReconfigurarHabito = (id: string) =>
  useMutacionHabitos((body: ReconfigurarHabitoRequest) => habitsApi.reconfigurar(id, body))
