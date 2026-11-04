import { calculadoraSchema } from './schemas'
import type { DatosCorporales } from './types'

// Superficie del catálogo para otras features (frontend/AGENTS.md: una feature solo usa de otra
// su api.ts/types.ts). Hoy el catálogo es estático; cuando el backend tenga plantillas de plan
// (mejora futura), este módulo pasará a consultarlo sin que cambien quienes lo usan.
export { buscarMeta, etiquetaArea, METAS_CATALOGO, type MetaCatalogo } from './catalogo'
export { INFO_LEGAL, VERSION_PRIVACIDAD, VERSION_TERMINOS } from './legal'

// Rutas de los documentos que se aceptan en el alta (ConsentFieldset).
export const RUTAS_LEGALES = {
  terminos: '/legal/terminos',
  salud: '/legal/salud',
  privacidad: '/legal/privacidad',
} as const

// Los datos de la calculadora llegan por location.state o desde localStorage: se validan con
// los mismos límites antes de usarlos y cualquier otra cosa se descarta.
export function validarDatosCorporales(valor: unknown): DatosCorporales | null {
  const resultado = calculadoraSchema.safeParse(valor)
  return resultado.success ? resultado.data : null
}
