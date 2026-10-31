// Contrato de HabitsController (/api/habits). Ver HabitTracker.Application/DTOs.
export const PERIODICIDADES = ['Diaria', 'Semanal'] as const
export type Periodicidad = (typeof PERIODICIDADES)[number]

export const ESTADOS_HABITO = ['Planificado', 'Activo', 'Pausado', 'Completado'] as const
export type EstadoHabito = (typeof ESTADOS_HABITO)[number]

export interface RegistroAvanceDto {
  id: string
  cantidad: number
  fecha: string
  unidad: string
}

export interface HabitoDto {
  id: string
  nombre: string
  periodicidad: Periodicidad
  cantidadObjetivo: number
  unidad: string
  estado: EstadoHabito
  cumpleMetaFinal: boolean
  fechaInicioCiclo: string
  registros: RegistroAvanceDto[]
}

export interface CrearHabitoRequest {
  nombre: string
  periodicidad: Periodicidad
  cantidadObjetivo: number
  unidad: string
  metaId: string
}

export interface TransicionHabitoRequest {
  nuevoEstado: EstadoHabito
}

export interface RegistrarAvanceRequest {
  cantidad: number
  fechaUtc: string
}

export interface ReconfigurarHabitoRequest {
  periodicidad: Periodicidad
  cantidadObjetivo: number
  unidad: string
}

// Permiso que exige POST /api/habits/{id}/advanced-reconfig (HabitoService).
export const PERMISO_RECONFIGURACION = 'Habits.AdvancedReconfig'
