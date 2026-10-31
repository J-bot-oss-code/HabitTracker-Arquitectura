import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { api, type IdResponse, type MensajeResponse } from '@/shared/api/http'
import { esErrorHttp } from '@/shared/api/problem'
import type {
  CrearSolicitudRequest,
  PermisoEfectivoResponse,
  RechazarSolicitudRequest,
  SolicitudPermisoDto,
} from './types'

const base = '/api/permissions'

export const permissionsApi = {
  // Administrador ve todas; Estándar solo las propias (lo decide el servidor).
  // Se envía siempre: en el backend soloPendientes vale true si se omite.
  listar: (soloPendientes = false) =>
    api.get<SolicitudPermisoDto[]>(base, { query: { soloPendientes } }),
  obtener: (id: string) => api.get<SolicitudPermisoDto>(`${base}/${id}`),
  crear: (body: CrearSolicitudRequest) => api.post<IdResponse>(base, body),
  aprobar: (id: string) => api.post<MensajeResponse>(`${base}/${id}/approve`),
  rechazar: (id: string, body: RechazarSolicitudRequest) =>
    api.post<MensajeResponse>(`${base}/${id}/reject`, body),
  // 200 → el permiso está aplicado; 403 → no lo está.
  esEfectivo: async (permiso: string): Promise<boolean> => {
    try {
      const r = await api.get<PermisoEfectivoResponse>(
        `${base}/effective/${encodeURIComponent(permiso)}`,
      )
      return r.efectivo
    } catch (error) {
      if (esErrorHttp(error, 403)) return false
      throw error
    }
  },
}

export const permissionKeys = {
  todas: ['permissions'] as const,
  lista: (soloPendientes: boolean) => ['permissions', 'list', { soloPendientes }] as const,
  detalle: (id: string) => ['permissions', 'detail', id] as const,
  efectivo: (permiso: string) => ['permissions', 'effective', permiso] as const,
}

export function useSolicitudes(soloPendientes = false) {
  return useQuery({
    queryKey: permissionKeys.lista(soloPendientes),
    queryFn: () => permissionsApi.listar(soloPendientes),
  })
}

export function useSolicitud(id: string) {
  return useQuery({
    queryKey: permissionKeys.detalle(id),
    queryFn: () => permissionsApi.obtener(id),
  })
}

export function usePermisoEfectivo(permiso: string) {
  return useQuery({
    queryKey: permissionKeys.efectivo(permiso),
    queryFn: () => permissionsApi.esEfectivo(permiso),
  })
}

function useMutacionPermisos<TVariables, TData>(mutationFn: (v: TVariables) => Promise<TData>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSettled: () => queryClient.invalidateQueries({ queryKey: permissionKeys.todas }),
  })
}

export const useCrearSolicitud = () => useMutacionPermisos(permissionsApi.crear)
export const useAprobarSolicitud = () => useMutacionPermisos(permissionsApi.aprobar)
export const useRechazarSolicitud = () =>
  useMutacionPermisos(({ id, motivoRechazo }: { id: string; motivoRechazo: string }) =>
    permissionsApi.rechazar(id, { motivoRechazo }),
  )
