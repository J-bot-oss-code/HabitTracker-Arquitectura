import { keepPreviousData, useMutation, useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/http'
import { miActividadACsv, descargarCsv, nombreCsv } from './csv'
import { descargarJson, miActividadAJson, nombreJson } from './exportarActividad'
import type { MetricasDto } from './metricas'
import {
  TAMANO_MAXIMO_ACTIVIDAD,
  type DiasActividad,
  type EventoAuditoriaDto,
  type FiltroAuditoria,
  type PaginaAuditoriaDto,
} from './types'

// /api/audit, /api/me/activity (types.ts) y /api/admin/metrics (metricas.ts) son endpoints reales.
export const auditoriaApi = {
  listar: (filtro: FiltroAuditoria = {}) =>
    api.get<PaginaAuditoriaDto>('/api/audit', { query: { ...filtro } }),
  miActividad: (dias: DiasActividad, pagina: number, tamano: number) =>
    api.get<PaginaAuditoriaDto>('/api/me/activity', { query: { dias, pagina, tamano } }),
  metricas: () => api.get<MetricasDto>('/api/admin/metrics'),
}

export const auditoriaKeys = {
  lista: (filtro: FiltroAuditoria) => ['auditoria', filtro] as const,
  miActividad: (dias: DiasActividad, pagina: number, tamano: number) =>
    ['auditoria', 'mi-actividad', dias, pagina, tamano] as const,
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

// Lo que hizo la persona en los últimos `dias`, una página a la vez. Al cambiar de página se conserva la
// anterior mientras llega la nueva; al cambiar de periodo NO (la lista vieja diría otros días que el título).
export const useMiActividad = (dias: DiasActividad, pagina: number, tamano: number) =>
  useQuery({
    queryKey: auditoriaKeys.miActividad(dias, pagina, tamano),
    queryFn: () => auditoriaApi.miActividad(dias, pagina, tamano),
    staleTime: 0,
    placeholderData: (previa, consultaPrevia) =>
      consultaPrevia?.queryKey[2] === dias ? previa : undefined,
  })

// Páginas como máximo al exportar: con 200 por página son 20 000 registros, más de lo que una persona
// genera en 30 días; evita un ciclo si el servidor devolviera un total inconsistente.
const MAXIMO_PAGINAS_EXPORTACION = 100

// TODOS los registros del periodo, página por página (200 por petición). Sin repetidos: si se registra algo
// mientras se pide, las páginas se desplazan y un mismo registro podría llegar dos veces.
export async function todaMiActividad(dias: DiasActividad): Promise<EventoAuditoriaDto[]> {
  const vistos = new Map<string, EventoAuditoriaDto>()
  for (let pagina = 1; pagina <= MAXIMO_PAGINAS_EXPORTACION; pagina++) {
    const respuesta = await auditoriaApi.miActividad(dias, pagina, TAMANO_MAXIMO_ACTIVIDAD)
    for (const registro of respuesta.registros) vistos.set(registro.id, registro)
    if (respuesta.registros.length === 0 || pagina * respuesta.tamano >= respuesta.total) break
  }
  return [...vistos.values()]
}

export type FormatoExportacion = 'csv' | 'json'

// Guarda el periodo completo en el equipo. El archivo no se devuelve: así no queda en la caché de consultas.
export function useExportarMiActividad() {
  return useMutation({
    mutationFn: async ({
      dias,
      formato,
    }: {
      dias: DiasActividad
      formato: FormatoExportacion
    }): Promise<void> => {
      const registros = await todaMiActividad(dias)
      const prefijo = `mi-actividad-${dias}-dias`
      if (formato === 'csv') descargarCsv(nombreCsv(prefijo), miActividadACsv(registros))
      else descargarJson(nombreJson(prefijo), miActividadAJson(registros, dias))
    },
  })
}

export const useMetricas = () =>
  useQuery({ queryKey: auditoriaKeys.metricas, queryFn: auditoriaApi.metricas, staleTime: 0 })
