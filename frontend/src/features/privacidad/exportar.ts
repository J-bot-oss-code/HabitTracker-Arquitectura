import { descargarArchivo } from '@/shared/lib/descarga'
import { hoyUtcInput } from '@/shared/lib/fechas'
import type { ExportacionServidor } from './types'

// Archivo de portabilidad "Descargar mis datos". Todo lo entrega el servidor (GET /api/privacy/export): cuenta,
// consentimientos, vínculos, sesiones y los módulos, incluida la sección perfilYSalud (perfil, cuestionario de salud
// descifrado, pesos y medidas). El formato lleva versión para poder evolucionar sin romper a quien lo procese.
export interface Exportacion extends ExportacionServidor {
  formato: 'habittracker-datos'
  version: 3
}

export function armarExportacion(servidor: ExportacionServidor): Exportacion {
  return { formato: 'habittracker-datos', version: 3, ...servidor }
}

// "habittracker-mis-datos-2026-11-03.json": el día es el día UTC, igual que el resto.
export function nombreArchivo(ahora = new Date()): string {
  return `habittracker-mis-datos-${hoyUtcInput(ahora)}.json`
}

export function descargarJson(nombre: string, contenido: unknown) {
  descargarArchivo(nombre, [JSON.stringify(contenido, null, 2)], 'application/json')
}
