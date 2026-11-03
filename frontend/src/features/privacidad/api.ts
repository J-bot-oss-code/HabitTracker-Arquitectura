import { useMutation } from '@tanstack/react-query'
import { authApi } from '@/features/auth/api'
import { habitsApi } from '@/features/habits/api'
import { perfilApi } from '@/features/perfil/api'
import { permissionsApi } from '@/features/permissions/api'
import { progresoApi } from '@/features/progreso/api'
import { api } from '@/shared/api/http'
import { esErrorHttp, esNoDisponible } from '@/shared/api/problem'
import { armarExportacion, descargarJson, nombreArchivo } from './exportar'
import type { SolicitudBajaRequest, SolicitudBajaResponse } from './types'

// Contratos propuestos (sin backend todavía): ver types.ts.
export const privacidadApi = {
  registrarExportacion: () => api.post<void>('/api/privacy/export-request'),
  solicitarBaja: (body: SolicitudBajaRequest) =>
    api.post<SolicitudBajaResponse>('/api/privacy/deletion-request', body),
}

// Una pieza que el servidor aún no tiene (o que el usuario todavía no creó) no impide la
// descarga: sale como null en el archivo. Cualquier otro fallo sí la interrumpe, para no
// entregar un archivo incompleto sin avisar.
async function opcional<T>(pedir: () => Promise<T>): Promise<T | null> {
  try {
    return await pedir()
  } catch (error) {
    if (esNoDisponible(error) || esErrorHttp(error, 404)) return null
    throw error
  }
}

// Portabilidad (docs/mejoras §7.1): reúne lo que la API ya entrega al usuario y lo baja como
// JSON. El servidor solo recibe el aviso para la auditoría; los datos no se suben a ningún lado.
export function useDescargarDatos() {
  return useMutation({
    // No devuelve el archivo: así no queda en la caché de consultas (son datos de salud).
    mutationFn: async (): Promise<void> => {
      const [cuenta, habitos, permisos, perfil, pesos, mediciones, consentimientos] =
        await Promise.all([
          authApi.me(),
          habitsApi.listar(),
          permissionsApi.listar(false),
          opcional(perfilApi.obtener),
          opcional(progresoApi.pesos),
          opcional(progresoApi.mediciones),
          opcional(perfilApi.consentimientos),
        ])
      const exportacion = armarExportacion({
        cuenta,
        habitos,
        permisos,
        perfil,
        pesos,
        mediciones,
        consentimientos,
      })
      descargarJson(nombreArchivo(), exportacion)
      // La constancia para la auditoría es de mejor esfuerzo y va después de la descarga: no
      // debe impedirla ni quedar registrada si la descarga falló.
      await privacidadApi.registrarExportacion().catch(() => undefined)
    },
  })
}

export const useSolicitarBaja = () => useMutation({ mutationFn: privacidadApi.solicitarBaja })
