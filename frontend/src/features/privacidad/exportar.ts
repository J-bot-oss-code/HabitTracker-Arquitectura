import type { MeResponse } from '@/features/auth/types'
import type { HabitoDto } from '@/features/habits/types'
import type { ConsentimientosDto, PerfilDto } from '@/features/perfil/types'
import type { SolicitudPermisoDto } from '@/features/permissions/types'
import type { MedicionDto, PesosDto } from '@/features/progreso/types'
import { descargarArchivo } from '@/shared/lib/descarga'
import { hoyUtcInput } from '@/shared/lib/fechas'

// Archivo de portabilidad "Descargar mis datos". Las claves van en español y el formato lleva
// versión para poder evolucionar sin romper a quien lo procese.
export interface Exportacion {
  formato: 'habittracker-datos'
  version: 1
  // Instante UTC (RD-11).
  generadoEn: string
  cuenta: MeResponse
  habitos: HabitoDto[]
  permisos: SolicitudPermisoDto[]
  // null: el usuario aún no tiene esos datos o el servidor todavía no los ofrece.
  perfil: PerfilDto | null
  progreso: { pesos: PesosDto | null; mediciones: MedicionDto[] | null }
  consentimientos: ConsentimientosDto | null
}

export function armarExportacion(
  datos: {
    cuenta: MeResponse
    habitos: HabitoDto[]
    permisos: SolicitudPermisoDto[]
    perfil: PerfilDto | null
    pesos: PesosDto | null
    mediciones: MedicionDto[] | null
    consentimientos: ConsentimientosDto | null
  },
  ahora = new Date(),
): Exportacion {
  return {
    formato: 'habittracker-datos',
    version: 1,
    generadoEn: ahora.toISOString(),
    cuenta: datos.cuenta,
    habitos: datos.habitos,
    permisos: datos.permisos,
    perfil: datos.perfil,
    progreso: { pesos: datos.pesos, mediciones: datos.mediciones },
    consentimientos: datos.consentimientos,
  }
}

// "habittracker-mis-datos-2026-11-03.json": el día es el día UTC, igual que el resto.
export function nombreArchivo(ahora = new Date()): string {
  return `habittracker-mis-datos-${hoyUtcInput(ahora)}.json`
}

export function descargarJson(nombre: string, contenido: unknown) {
  descargarArchivo(nombre, [JSON.stringify(contenido, null, 2)], 'application/json')
}
