// Contrato de ReportsController (/api/reports/permissions y /adherence). Ver Reports.Application/DTOs.
import type { EstadoSolicitudPermiso } from '@/features/permissions/types'

// Días AAAA-MM-DD, ambos incluidos, UTC.
export interface RangoReporte {
  desde: string
  hasta: string
}

// «propio»: el Estándar ve solo lo suyo. «todos»: el Administrador, de todas las personas.
export type AlcanceReporte = 'propio' | 'todos'

export interface FilaPermisosDto {
  estado: EstadoSolicitudPermiso
  cantidad: number
}

// Una fila por estado que tenga solicitudes en el rango.
export interface ReportePermisosDto extends RangoReporte {
  alcance: AlcanceReporte
  filas: FilaPermisosDto[]
  total: number
}

// Estándar: un plan suyo (personas = 1, días enteros). Administrador: un plan del catálogo con las
// personas que lo siguieron y los días en promedio (un decimal), sin datos personales.
export interface FilaConstanciaDto {
  plan: string
  personas: number
  diasConAvance: number
  diasSoloMinimo: number
  diasEnRango: number
  // null si no hubo días en marcha.
  porcentajeConstancia: number | null
}

export interface ReporteConstanciaDto extends RangoReporte {
  alcance: AlcanceReporte
  // «¿Qué tan constante fui en cada plan durante este período?»
  pregunta: string
  filas: FilaConstanciaDto[]
}
