import { useCrearSolicitud, useSolicitudes } from '@/features/permissions/api'
import { claveEliminarArchivo } from './types'

export type EstadoPermisoArchivo = 'aplicado' | 'pendiente' | 'rechazado' | 'ninguno'

// El permiso para eliminar un archivo de un plan en curso es uno por archivo. Una sola lista de solicitudes (en caché,
// compartida) dice cuáles están aplicadas o pendientes; quien decide es el servidor (RD-06): esto solo orienta.
export function usePermisosDeArchivos() {
  const solicitudes = useSolicitudes(false)
  const crear = useCrearSolicitud()

  const estadoDe = (documentoId: string): EstadoPermisoArchivo => {
    const clave = claveEliminarArchivo(documentoId)
    const propias = (solicitudes.data ?? []).filter(
      (s) => s.permisoSolicitado.toLowerCase() === clave.toLowerCase(),
    )
    if (propias.some((s) => s.estado === 'Aplicada')) return 'aplicado'
    if (propias.some((s) => s.estado === 'Pendiente')) return 'pendiente'
    if (propias.some((s) => s.estado === 'Rechazada')) return 'rechazado'
    return 'ninguno'
  }

  return {
    estadoDe,
    cargando: solicitudes.isPending,
    pedir: crear.mutate,
    pidiendo: crear.isPending,
    errorAlPedir: crear.error,
    claveDe: claveEliminarArchivo,
  }
}
