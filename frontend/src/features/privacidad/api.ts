import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { perfilApi } from '@/features/perfil/api'
import { progresoApi } from '@/features/progreso/api'
import { api } from '@/shared/api/http'
import { esErrorHttp, esNoDisponible } from '@/shared/api/problem'
import { armarExportacion, descargarJson, nombreArchivo } from './exportar'
import type {
  BajaProgramadaResponse,
  ExportacionServidor,
  SolicitudBajaRequest,
  SolicitudBajaResponse,
} from './types'

const base = '/api/privacy'

export const privacidadApi = {
  exportar: () => api.get<ExportacionServidor>(`${base}/export`),
  solicitarBaja: (body: SolicitudBajaRequest) =>
    api.post<SolicitudBajaResponse>(`${base}/deletion-request`, body),
  bajaProgramada: () => api.get<BajaProgramadaResponse>(`${base}/deletion-request`),
  cancelarBaja: () => api.delete<void>(`${base}/deletion-request`),
}

export const privacidadKeys = {
  baja: ['privacidad', 'baja'] as const,
}

// Una pieza que el servidor aún no tiene (o que la persona todavía no creó) no impide la descarga: sale como null en el
// archivo. Cualquier otro fallo sí la interrumpe, para no entregar un archivo incompleto sin avisar.
async function opcional<T>(pedir: () => Promise<T>): Promise<T | null> {
  try {
    return await pedir()
  } catch (error) {
    if (esNoDisponible(error) || esErrorHttp(error, 404)) return null
    throw error
  }
}

// Portabilidad (Ley 172-13 y GDPR): el servidor reúne todo lo que guarda de la persona (y deja la constancia en la
// auditoría); aquí solo se baja como JSON. Los datos no se suben a ningún lado.
export function useDescargarDatos() {
  return useMutation({
    // No devuelve el archivo: así no queda en la caché de consultas (son datos de salud).
    mutationFn: async (): Promise<void> => {
      const [servidor, perfil, pesos, mediciones] = await Promise.all([
        privacidadApi.exportar(),
        opcional(perfilApi.obtener),
        opcional(progresoApi.pesos),
        opcional(progresoApi.mediciones),
      ])
      descargarJson(nombreArchivo(), armarExportacion(servidor, { perfil, pesos, mediciones }))
    },
  })
}

export const useSolicitarBaja = () => useMutation({ mutationFn: privacidadApi.solicitarBaja })

// Fecha del borrado si la persona pidió eliminar su cuenta y sigue en el periodo de gracia; null si no hay baja.
export function useBajaProgramada(habilitado = true) {
  return useQuery({
    queryKey: privacidadKeys.baja,
    enabled: habilitado,
    queryFn: async () => {
      try {
        return (await privacidadApi.bajaProgramada()).borradoEn
      } catch (error) {
        if (esErrorHttp(error, 404)) return null
        throw error
      }
    },
  })
}

export function useCancelarBaja() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: privacidadApi.cancelarBaja,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: privacidadKeys.baja }),
  })
}
