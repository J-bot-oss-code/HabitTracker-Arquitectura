// Contrato de HabitsController (/api/habits). Ver HabitTracker.Application/DTOs.
// Solo el tipo: planes/types.ts también toma de aquí la periodicidad y el ciclo no llega al código.
import type { Ancla } from '@/features/planes/types'

export const PERIODICIDADES = ['Diaria', 'Semanal'] as const
export type Periodicidad = (typeof PERIODICIDADES)[number]

export const ESTADOS_HABITO = ['Planificado', 'Activo', 'Pausado', 'Completado'] as const
export type EstadoHabito = (typeof ESTADOS_HABITO)[number]

export interface RegistroAvanceDto {
  id: string
  cantidad: number
  fecha: string
  unidad: string
  // "Hoy solo lo mínimo" (Fase 4). El backend siempre lo envía (falso por defecto).
  esVersionMinima?: boolean
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
  // Desde la Fase 4 (Mis planes). El backend siempre los envía, con null en un hábito suelto.
  // Plan de la persona al que pertenece el hábito (GET /api/my-plans/{id}).
  planUsuarioId?: string | null
  // Instrucciones copiadas de la tarea del plan.
  descripcion?: string | null
  // Lo que cuenta en un mal día. Sin ella no se puede registrar un avance con esVersionMinima.
  versionMinima?: string | null
  // Momento de la jornada al que se ata (la hora concreta la da GET /api/today).
  ancla?: Ancla | null
}

export interface CrearHabitoRequest {
  nombre: string
  periodicidad: Periodicidad
  cantidadObjetivo: number
  unidad: string
  // Opcional: un hábito puede ir sin meta del catálogo (HabitTracker v2, Fase 1).
  metaId?: string
}

export interface TransicionHabitoRequest {
  nuevoEstado: EstadoHabito
}

export interface RegistrarAvanceRequest {
  cantidad: number
  fechaUtc: string
  // Opcional (falso por defecto). Solo vale en un hábito de un plan: en uno suelto el backend responde
  // 400 version_minima_no_disponible.
  esVersionMinima?: boolean
}

export interface ReconfigurarHabitoRequest {
  periodicidad: Periodicidad
  cantidadObjetivo: number
  unidad: string
}

// Permiso que exige POST /api/habits/{id}/advanced-reconfig (HabitoService).
export const PERMISO_RECONFIGURACION = 'Habits.AdvancedReconfig'
