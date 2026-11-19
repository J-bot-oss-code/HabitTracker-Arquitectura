import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/http'
import type { RangoReporte, ReporteConstanciaDto, ReportePermisosDto } from './types'

const base = '/api/reports'

// Cualquier rol con sesión: el alcance (propio o todos) lo decide el rol del token, no el cliente.
// `desde` y `hasta` son obligatorios; sin ellos, invertidos o con más de 366 días el servidor responde 400.
export const reportesApi = {
  permisos: (rango: RangoReporte) =>
    api.get<ReportePermisosDto>(`${base}/permissions`, { query: { ...rango } }),
  constancia: (rango: RangoReporte) =>
    api.get<ReporteConstanciaDto>(`${base}/adherence`, { query: { ...rango } }),
}

const SIN_RANGO: RangoReporte = { desde: '', hasta: '' }

export const reportesKeys = {
  permisos: (rango: RangoReporte) => ['reportes', 'permisos', rango] as const,
  constancia: (rango: RangoReporte) => ['reportes', 'constancia', rango] as const,
}

// `rango` null: aún no hay un rango válido (el formulario no se ha enviado) y no se consulta.
export function usePermisosReporte(rango: RangoReporte | null) {
  return useQuery({
    queryKey: reportesKeys.permisos(rango ?? SIN_RANGO),
    queryFn: () => reportesApi.permisos(rango!),
    enabled: rango !== null,
    placeholderData: keepPreviousData,
  })
}

export function useConstanciaReporte(rango: RangoReporte | null) {
  return useQuery({
    queryKey: reportesKeys.constancia(rango ?? SIN_RANGO),
    queryFn: () => reportesApi.constancia(rango!),
    enabled: rango !== null,
    placeholderData: keepPreviousData,
  })
}
