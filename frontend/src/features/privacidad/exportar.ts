import type { PerfilDto } from '@/features/perfil/types'
import type { MedicionDto, PesosDto } from '@/features/progreso/types'
import { descargarArchivo } from '@/shared/lib/descarga'
import { hoyUtcInput } from '@/shared/lib/fechas'
import type { ExportacionServidor } from './types'

// Archivo de portabilidad "Descargar mis datos". Lo esencial lo entrega el servidor (GET /api/privacy/export:
// cuenta, consentimientos, vínculos, sesiones y los módulos); el perfil físico, el peso y las medidas todavía no
// existen en el backend real y se añaden solo si la API simulada los ofrece. El formato lleva versión para poder
// evolucionar sin romper a quien lo procese.
export interface Exportacion extends ExportacionServidor {
  formato: 'habittracker-datos'
  version: 2
  // null: la persona aún no tiene esos datos o el servidor todavía no los ofrece.
  perfil: PerfilDto | null
  progreso: { pesos: PesosDto | null; mediciones: MedicionDto[] | null }
}

export function armarExportacion(
  servidor: ExportacionServidor,
  extras: { perfil: PerfilDto | null; pesos: PesosDto | null; mediciones: MedicionDto[] | null },
): Exportacion {
  return {
    formato: 'habittracker-datos',
    version: 2,
    ...servidor,
    perfil: extras.perfil,
    progreso: { pesos: extras.pesos, mediciones: extras.mediciones },
  }
}

// "habittracker-mis-datos-2026-11-03.json": el día es el día UTC, igual que el resto.
export function nombreArchivo(ahora = new Date()): string {
  return `habittracker-mis-datos-${hoyUtcInput(ahora)}.json`
}

export function descargarJson(nombre: string, contenido: unknown) {
  descargarArchivo(nombre, [JSON.stringify(contenido, null, 2)], 'application/json')
}
