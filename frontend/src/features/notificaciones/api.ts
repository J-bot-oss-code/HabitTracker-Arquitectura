import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api } from '@/shared/api/http'
import type { NotificacionDto, PreferenciasNotificacionesDto } from './types'

const base = '/api/notifications'

// Cualquier rol con sesión; el servidor devuelve solo las propias, de la más reciente a la más antigua.
export const notificacionesApi = {
  // Se envía siempre: el servidor lee un bool y, sin él, entiende false.
  listar: (soloNoLeidas: boolean) => api.get<NotificacionDto[]>(base, { query: { soloNoLeidas } }),
  // 204. Una ajena es 403; una que no existe, 404 con codigo notificacion_no_encontrada.
  marcarLeida: (id: string) => api.post<void>(`${base}/${id}/read`),
  preferencias: () => api.get<PreferenciasNotificacionesDto>(`${base}/preferences`),
  guardarPreferencias: (correoActivo: boolean) =>
    api.put<PreferenciasNotificacionesDto>(`${base}/preferences`, { correoActivo }),
}

export const notificacionesKeys = {
  todas: ['notificaciones'] as const,
  lista: (soloNoLeidas: boolean) => ['notificaciones', 'lista', { soloNoLeidas }] as const,
  preferencias: ['notificaciones', 'preferencias'] as const,
}

export function useNotificaciones(soloNoLeidas = false, habilitado = true) {
  return useQuery({
    queryKey: notificacionesKeys.lista(soloNoLeidas),
    queryFn: () => notificacionesApi.listar(soloNoLeidas),
    enabled: habilitado,
  })
}

// Para el globo del menú. El backend no tiene un endpoint de conteo: se pide la lista de no leídas (la
// misma consulta que useNotificaciones(true), compartida en la caché) y se cuenta. Se refresca cada minuto.
export function useContadorNoLeidas(habilitado = true) {
  return useQuery({
    queryKey: notificacionesKeys.lista(true),
    queryFn: () => notificacionesApi.listar(true),
    select: (lista) => lista.length,
    enabled: habilitado,
    refetchInterval: 60_000,
  })
}

export function useMarcarLeida() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: notificacionesApi.marcarLeida,
    // También cuando el servidor rechaza: la lista en caché pudo quedar vieja.
    onSettled: () =>
      queryClient.invalidateQueries({ queryKey: [...notificacionesKeys.todas, 'lista'] }),
  })
}

export function usePreferenciasNotificaciones(habilitado = true) {
  return useQuery({
    queryKey: notificacionesKeys.preferencias,
    queryFn: notificacionesApi.preferencias,
    enabled: habilitado,
  })
}

export function useGuardarPreferencias() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: notificacionesApi.guardarPreferencias,
    onSuccess: (preferencias) =>
      queryClient.setQueryData(notificacionesKeys.preferencias, preferencias),
  })
}
