import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type IdResponse } from '@/shared/api/http'
import { ApiError } from '@/shared/api/problem'
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
  obtener: (id: string) => api.get<HabitoDto>(`${base}/${id}`),
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
  // Prefijo de `todos`: invalidar la lista también refresca el detalle.
  detalle: (id: string) => ['habits', id] as const,
}

export function useHabitos() {
  return useQuery({ queryKey: habitKeys.todos, queryFn: habitsApi.listar })
}

const ES_GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// Un hábito que el backend confirma que no existe (404 con codigo habito_no_encontrado) o que es de otra
// persona (403) se muestra como "no encontrado". Un 404 SIN cuerpo no es eso: es un endpoint que no existe
// (esNoDisponible) y sigue siendo un error de carga.
function esHabitoNoEncontrado(error: unknown): boolean {
  if (!(error instanceof ApiError)) return false
  if (error.status === 403) return true
  return error.status === 404 && error.problem?.codigo === 'habito_no_encontrado'
}

// GET /api/habits/{id}. Un id que no es GUID ni se consulta: la ruta {id:guid} del backend no lo atiende.
export function useHabito(id: string) {
  return useQuery({
    queryKey: habitKeys.detalle(id),
    queryFn: async () => {
      if (!ES_GUID.test(id)) return null
      try {
        return await habitsApi.obtener(id)
      } catch (error) {
        if (esHabitoNoEncontrado(error)) return null
        throw error
      }
    },
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

// Métricas de presentación de Progreso, para otras features (frontend/AGENTS.md: una feature
// solo usa de otra su api.ts/types.ts).
export {
  constanciaPorDia,
  logrosDe,
  recordsDe,
  type DiaConstancia,
  type Logro,
  type NivelConstancia,
  type Records,
} from './progreso'
