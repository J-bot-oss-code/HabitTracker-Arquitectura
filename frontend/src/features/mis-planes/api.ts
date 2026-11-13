import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { CLAVES_SEGUIMIENTO, invalidarSeguimiento } from '@/shared/api/clavesSeguimiento'
import { api } from '@/shared/api/http'
import { ApiError } from '@/shared/api/problem'
import type {
  AjusteAplicable,
  CambiarEstadoPlanRequest,
  HoyDto,
  InscribirseRequest,
  PlanUsuarioDto,
  RecordatorioPlanRequest,
  ResumenProgresoDto,
} from './types'

const base = '/api/my-plans'

// MyPlansController y TodayController: solo el rol Estándar (el Administrador recibe 403).
export const misPlanesApi = {
  // 201 con el plan y sus hábitos ya activos.
  inscribirse: (body: InscribirseRequest) => api.post<PlanUsuarioDto>(base, body),
  // Del más reciente al más antiguo, también los completados y cancelados.
  listar: () => api.get<PlanUsuarioDto[]>(base),
  obtener: (id: string) => api.get<PlanUsuarioDto>(`${base}/${id}`),
  cambiarEstado: (id: string, body: CambiarEstadoPlanRequest) =>
    api.post<PlanUsuarioDto>(`${base}/${id}/transition`, body),
  ajustar: (id: string, tipo: AjusteAplicable) =>
    api.post<PlanUsuarioDto>(`${base}/${id}/adjust`, { tipo }),
  configurarRecordatorio: (id: string, body: RecordatorioPlanRequest) =>
    api.put<PlanUsuarioDto>(`${base}/${id}/reminder`, body),
  hoy: () => api.get<HoyDto>('/api/today'),
  resumen: () => api.get<ResumenProgresoDto>('/api/progress/summary'),
  // 204: cada logro se felicita una sola vez.
  marcarLogrosVistos: () => api.post<void>('/api/progress/achievements/seen'),
}

export const misPlanesKeys = {
  todos: CLAVES_SEGUIMIENTO.misPlanes,
  lista: [...CLAVES_SEGUIMIENTO.misPlanes, 'lista'] as const,
  detalle: (id: string) => [...CLAVES_SEGUIMIENTO.misPlanes, 'detalle', id] as const,
  hoy: CLAVES_SEGUIMIENTO.hoy,
  resumen: CLAVES_SEGUIMIENTO.resumen,
}

const ES_GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

// 404 con codigo plan_usuario_no_encontrado: el plan no existe o es de otra persona (no se distingue).
// Un 404 SIN cuerpo no es eso: es un endpoint que no existe (esNoDisponible) y sigue siendo un error.
export function esPlanNoEncontrado(error: unknown): boolean {
  return (
    error instanceof ApiError &&
    error.status === 404 &&
    error.problem?.codigo === 'plan_usuario_no_encontrado'
  )
}

export function useMisPlanes(habilitado = true) {
  return useQuery({
    queryKey: misPlanesKeys.lista,
    queryFn: misPlanesApi.listar,
    enabled: habilitado,
  })
}

// GET /api/my-plans/{id}. `null`: el plan no existe o no es de la persona. Un id que no es GUID ni se
// consulta: la ruta {id:guid} del backend no lo atiende.
export function useMiPlan(id: string) {
  return useQuery({
    queryKey: misPlanesKeys.detalle(id),
    queryFn: async () => {
      if (!ES_GUID.test(id)) return null
      try {
        return await misPlanesApi.obtener(id)
      } catch (error) {
        if (esPlanNoEncontrado(error)) return null
        throw error
      }
    },
  })
}

// Las tareas de hoy, el mensaje del día, la celebración de la semana y los logros por celebrar. El día es
// el local de la persona: se vuelve a pedir al volver a la pantalla.
export function useHoy(habilitado = true) {
  return useQuery({
    queryKey: misPlanesKeys.hoy,
    queryFn: misPlanesApi.hoy,
    enabled: habilitado,
    staleTime: 0,
  })
}

export function useResumenProgreso(habilitado = true) {
  return useQuery({
    queryKey: misPlanesKeys.resumen,
    queryFn: misPlanesApi.resumen,
    enabled: habilitado,
    staleTime: 0,
  })
}

// Inscribirse, cambiar de estado, ajustar o configurar el recordatorio cambian el plan, sus hábitos, Hoy
// y el resumen: se invalida todo el seguimiento (también cuando el servidor rechaza, por si el estado en
// caché ya no era el vigente).
function useMutacionPlanes<TVariables, TData>(mutationFn: (v: TVariables) => Promise<TData>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSettled: () => invalidarSeguimiento(queryClient),
  })
}

export const useInscribirse = () => useMutacionPlanes(misPlanesApi.inscribirse)

export const useCambiarEstadoPlan = (id: string) =>
  useMutacionPlanes((body: CambiarEstadoPlanRequest) => misPlanesApi.cambiarEstado(id, body))

export const useAjustarPlan = (id: string) =>
  useMutacionPlanes((tipo: AjusteAplicable) => misPlanesApi.ajustar(id, tipo))

export const useRecordatorioPlan = (id: string) =>
  useMutacionPlanes((body: RecordatorioPlanRequest) =>
    misPlanesApi.configurarRecordatorio(id, body),
  )

// Los logros ya celebrados dejan de venir en `logrosNuevos` de Hoy y pasan a `visto` en el resumen.
export function useMarcarLogrosVistos() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: misPlanesApi.marcarLogrosVistos,
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: misPlanesKeys.hoy }),
        queryClient.invalidateQueries({ queryKey: misPlanesKeys.resumen }),
      ]),
  })
}
