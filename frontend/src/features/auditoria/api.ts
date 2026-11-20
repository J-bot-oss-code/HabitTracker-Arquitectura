import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/http'
import type { MetricasDto } from './metricas'
import type { FiltroAuditoria, PaginaAuditoriaDto } from './types'

// /api/audit es real (types.ts); /api/admin/metrics sigue siendo un contrato propuesto (metricas.ts).
export const auditoriaApi = {
  listar: (filtro: FiltroAuditoria = {}) =>
    api.get<PaginaAuditoriaDto>('/api/audit', { query: { ...filtro } }),
  metricas: () => api.get<MetricasDto>('/api/admin/metrics'),
}

export const auditoriaKeys = {
  lista: (filtro: FiltroAuditoria) => ['auditoria', filtro] as const,
  metricas: ['auditoria', 'metricas'] as const,
}

export const useAuditoria = (filtro: FiltroAuditoria = {}) =>
  useQuery({
    queryKey: auditoriaKeys.lista(filtro),
    queryFn: () => auditoriaApi.listar(filtro),
    // Lo último primero y sin quedarse con datos viejos al volver a la pantalla; al filtrar se
    // conserva la tabla anterior mientras llega la nueva (sin parpadeo).
    staleTime: 0,
    placeholderData: keepPreviousData,
  })

export const useMetricas = () =>
  useQuery({ queryKey: auditoriaKeys.metricas, queryFn: auditoriaApi.metricas, staleTime: 0 })
