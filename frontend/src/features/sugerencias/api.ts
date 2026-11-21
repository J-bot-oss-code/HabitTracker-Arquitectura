import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/http'
import type {
  EnviarSugerenciaRequest,
  EstadoDestino,
  FiltroBandeja,
  PaginaSugerenciasDto,
  SugerenciaAdminDto,
  SugerenciaDto,
} from './types'

// Estándar: api/feedback (lo suyo). Administrador: api/admin/feedback (la bandeja). Cada rol recibe 403 en las rutas del otro.
export const sugerenciasApi = {
  // 201. Un texto fuera de 10 a 2000 es 400; con 5 envíos en 24 horas, 400 con codigo limite_sugerencias.
  enviar: (cuerpo: EnviarSugerenciaRequest) => api.post<SugerenciaDto>('/api/feedback', cuerpo),
  listarMias: () => api.get<SugerenciaDto[]>('/api/feedback'),
  bandeja: ({ estado, pagina, tamano }: FiltroBandeja) =>
    api.get<PaginaSugerenciasDto>('/api/admin/feedback', { query: { estado, pagina, tamano } }),
  // Repetir el mismo estado es inofensivo; una que no existe es 404 con codigo sugerencia_no_encontrada.
  cambiarEstado: (id: string, estado: EstadoDestino) =>
    api.post<SugerenciaAdminDto>(`/api/admin/feedback/${id}/status`, { estado }),
}

export const sugerenciasKeys = {
  todas: ['sugerencias'] as const,
  mias: ['sugerencias', 'mias'] as const,
  bandeja: (filtro: FiltroBandeja) => ['sugerencias', 'bandeja', filtro] as const,
  nuevas: ['sugerencias', 'bandeja', 'nuevas'] as const,
}

export function useMisSugerencias() {
  return useQuery({ queryKey: sugerenciasKeys.mias, queryFn: sugerenciasApi.listarMias })
}

export function useEnviarSugerencia() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: sugerenciasApi.enviar,
    // También si el servidor rechaza (p. ej. el tope diario): la lista en caché pudo quedar vieja.
    onSettled: () => queryClient.invalidateQueries({ queryKey: sugerenciasKeys.mias }),
  })
}

export function useBandejaSugerencias(filtro: FiltroBandeja) {
  return useQuery({
    queryKey: sugerenciasKeys.bandeja(filtro),
    queryFn: () => sugerenciasApi.bandeja(filtro),
    // Al cambiar de página o de filtro se sigue viendo la lista anterior (atenuada) en vez de un parpadeo.
    placeholderData: keepPreviousData,
    staleTime: 0,
  })
}

// Para el globo del menú: el servidor ya cuenta las nuevas (con cualquier filtro), así que basta una página de uno.
export function useContadorSugerenciasNuevas(habilitado = true) {
  return useQuery({
    queryKey: sugerenciasKeys.nuevas,
    queryFn: () => sugerenciasApi.bandeja({ pagina: 1, tamano: 1 }),
    select: (pagina) => pagina.nuevas,
    enabled: habilitado,
    refetchInterval: 60_000,
  })
}

export function useCambiarEstadoSugerencia() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, estado }: { id: string; estado: EstadoDestino }) =>
      sugerenciasApi.cambiarEstado(id, estado),
    // También cuando el servidor rechaza (p. ej. un 404): la bandeja y el contador pudieron quedar viejos.
    onSettled: () =>
      queryClient.invalidateQueries({ queryKey: [...sugerenciasKeys.todas, 'bandeja'] }),
  })
}
