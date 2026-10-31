import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import type { UsuarioResponseDto } from '@/features/auth/types'
import { api, type MensajeResponse } from '@/shared/api/http'
import type { Rol } from '@/shared/auth/roles'

// Contrato de AdminUsuariosController (/api/admin/usuarios), solo Administrador.
const base = '/api/admin/usuarios'

export const adminUsersApi = {
  listar: () => api.get<UsuarioResponseDto[]>(base),
  cambiarRol: (id: string, nuevoRol: Rol) =>
    api.patch<MensajeResponse>(`${base}/${id}/rol`, { nuevoRol }),
  cambiarEstado: (id: string, nuevoEstado: boolean) =>
    api.patch<MensajeResponse>(`${base}/${id}/estado`, { nuevoEstado }),
  forzarRestablecimiento: (id: string) =>
    api.post<MensajeResponse>(`${base}/${id}/forzar-restablecimiento`),
}

export const adminUserKeys = { todos: ['admin', 'usuarios'] as const }

export function useUsuarios() {
  return useQuery({ queryKey: adminUserKeys.todos, queryFn: adminUsersApi.listar })
}

function useMutacionUsuarios<TVariables>(mutationFn: (v: TVariables) => Promise<unknown>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSettled: () => queryClient.invalidateQueries({ queryKey: adminUserKeys.todos }),
  })
}

export const useCambiarRol = () =>
  useMutacionUsuarios(({ id, rol }: { id: string; rol: Rol }) => adminUsersApi.cambiarRol(id, rol))

export const useCambiarEstado = () =>
  useMutacionUsuarios(({ id, activo }: { id: string; activo: boolean }) =>
    adminUsersApi.cambiarEstado(id, activo),
  )

export const useForzarRestablecimiento = () =>
  useMutacionUsuarios(adminUsersApi.forzarRestablecimiento)
