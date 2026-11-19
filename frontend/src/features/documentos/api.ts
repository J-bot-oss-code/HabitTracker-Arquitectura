import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { permissionKeys } from '@/features/permissions/api'
import { api } from '@/shared/api/http'
import { codigoDeError } from '@/shared/api/problem'
import type { AdjuntoPlanDto, DocumentoDto, VinculoDocumentoDto } from './types'

export { claveEliminarArchivo } from './types'

const base = '/api/documents'
const basePlanes = '/api/my-plans'

// DocumentsController: cualquier rol autenticado; el Estándar solo ve y toca los suyos (otro archivo: 403 o 404).
export const documentosApi = {
  listar: () => api.get<DocumentoDto[]>(base),
  obtener: (id: string) => api.get<DocumentoDto>(`${base}/${id}`),
  // 201 con el registro. multipart/form-data con el campo `archivo` (el navegador pone el límite del multipart).
  subir: (archivo: File) => {
    const formulario = new FormData()
    formulario.append('archivo', archivo)
    return api.post<DocumentoDto>(base, formulario)
  },
  // El archivo se pide con el token y se baja como Blob: un enlace directo no llevaría la sesión.
  descargar: (id: string) => api.descargar(`${base}/${id}/download`),
  // 204 (borrado lógico). 409 documento_requiere_permiso si está en un plan en curso y no hay permiso aplicado.
  eliminar: (id: string) => api.delete<void>(`${base}/${id}`),
}

// AdjuntosPlanController: solo el rol Estándar. Un plan o archivo ajeno responde 404.
export const adjuntosApi = {
  vinculos: () => api.get<VinculoDocumentoDto[]>(`${basePlanes}/attachments`),
  deUnPlan: (planId: string) => api.get<AdjuntoPlanDto[]>(`${basePlanes}/${planId}/attachments`),
  // 204. 400 plan_no_en_curso | adjunto_duplicado | limite_adjuntos; 404 documento_no_encontrado.
  adjuntar: (planId: string, documentoId: string) =>
    api.post<void>(`${basePlanes}/${planId}/attachments`, { documentoId }),
  // 204. 409 documento_requiere_permiso con el plan en curso y sin el permiso aplicado.
  quitar: (planId: string, documentoId: string) =>
    api.delete<void>(`${basePlanes}/${planId}/attachments/${documentoId}`),
}

export const documentoKeys = {
  todos: ['documentos'] as const,
  lista: ['documentos', 'lista'] as const,
  vinculos: ['documentos', 'vinculos'] as const,
  delPlan: (planId: string) => ['documentos', 'plan', planId] as const,
}

// 409 documento_requiere_permiso: el archivo está en un plan en curso y falta el permiso de un Administrador.
export const requierePermisoDeArchivo = (error: unknown): boolean =>
  codigoDeError(error) === 'documento_requiere_permiso'

export function useDocumentos(habilitado = true) {
  return useQuery({
    queryKey: documentoKeys.lista,
    queryFn: documentosApi.listar,
    enabled: habilitado,
  })
}

export function useVinculos(habilitado = true) {
  return useQuery({
    queryKey: documentoKeys.vinculos,
    queryFn: adjuntosApi.vinculos,
    enabled: habilitado,
  })
}

export function useAdjuntosDePlan(planId: string, habilitado = true) {
  return useQuery({
    queryKey: documentoKeys.delPlan(planId),
    queryFn: () => adjuntosApi.deUnPlan(planId),
    enabled: habilitado,
  })
}

// Se invalida también cuando el servidor rechaza, por si lo que había en caché ya no era lo vigente.
export function useSubirDocumento() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: documentosApi.subir,
    onSettled: () => queryClient.invalidateQueries({ queryKey: documentoKeys.lista }),
  })
}

// Eliminar mueve la lista, los vínculos y los archivos de cada plan. También se refrescan los permisos: el
// 409 lleva a pedir el de ese archivo y la pantalla de permisos debe mostrar el estado vigente.
export function useEliminarDocumento() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: documentosApi.eliminar,
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: documentoKeys.todos }),
        queryClient.invalidateQueries({ queryKey: permissionKeys.todas }),
      ]),
  })
}

export function useAdjuntarArchivo(planId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (documentoId: string) => adjuntosApi.adjuntar(planId, documentoId),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: documentoKeys.vinculos }),
        queryClient.invalidateQueries({ queryKey: documentoKeys.delPlan(planId) }),
      ]),
  })
}

export function useQuitarAdjunto(planId: string) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (documentoId: string) => adjuntosApi.quitar(planId, documentoId),
    onSettled: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: documentoKeys.vinculos }),
        queryClient.invalidateQueries({ queryKey: documentoKeys.delPlan(planId) }),
        queryClient.invalidateQueries({ queryKey: permissionKeys.todas }),
      ]),
  })
}

// Baja el archivo y lo guarda con el nombre que propone el servidor (o `nombre`, el del registro).
export function useDescargarDocumento() {
  return useMutation({
    mutationFn: async ({ id, nombre }: { id: string; nombre: string }) => {
      const descarga = await documentosApi.descargar(id)
      guardarArchivo(descarga.blob, descarga.nombre ?? nombre)
    },
  })
}

function guardarArchivo(blob: Blob, nombre: string) {
  const url = URL.createObjectURL(blob)
  const enlace = document.createElement('a')
  enlace.href = url
  enlace.download = nombre
  document.body.append(enlace)
  enlace.click()
  enlace.remove()
  // Con un pequeño retraso: revocarlo al instante puede cortar la descarga en algunos navegadores.
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
