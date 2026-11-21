import { descargarArchivo } from '@/shared/lib/descarga'
import { hoyUtcInput } from '@/shared/lib/fechas'
import { aRegistroExportado, type RegistroExportado } from './detalle'
import type { DiasActividad, EventoAuditoriaDto } from './types'

// Copia de «Mi actividad» para guardar en el equipo. Lleva el formato y la versión para poder evolucionar sin
// romper a quien la procese. Cada registro tiene los mismos cuatro campos que el CSV (fecha en UTC con «Z»,
// acción, antes y después); sin identificadores internos ni correo.
export interface ExportacionActividad {
  formato: 'habittracker-mi-actividad'
  version: 1
  generadoEn: string
  dias: DiasActividad
  total: number
  registros: RegistroExportado[]
}

export function miActividadAJson(
  registros: EventoAuditoriaDto[],
  dias: DiasActividad,
  ahora = new Date(),
): ExportacionActividad {
  return {
    formato: 'habittracker-mi-actividad',
    version: 1,
    generadoEn: ahora.toISOString(),
    dias,
    total: registros.length,
    registros: registros.map(aRegistroExportado),
  }
}

// "mi-actividad-7-dias-2026-11-03.json": el día es el día UTC, igual que el del CSV.
export function nombreJson(prefijo: string, ahora = new Date()): string {
  return `${prefijo}-${hoyUtcInput(ahora)}.json`
}

export function descargarJson(nombre: string, contenido: ExportacionActividad) {
  descargarArchivo(nombre, [JSON.stringify(contenido, null, 2)], 'application/json')
}
