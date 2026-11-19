// Contrato de Host/WebApi (PlansController, HabitTracker v2 Fase 3). Los enums viajan como texto en
// PascalCase. El catálogo y el detalle son públicos; los recomendados son de cada persona.
import type { Periodicidad } from '@/features/habits/types'
import type {
  Alergia,
  CondicionSalud,
  Lugar,
  PerfilLaboral,
  ZonaCorporal,
} from '@/features/perfil/types'

export const AREAS_PLAN = [
  'ControlDePeso',
  'Gimnasio',
  'Estres',
  'Concentracion',
  'Descanso',
  'Hidratacion',
  'Nutricion',
  'Movilidad',
  'Energia',
] as const
export type AreaPlan = (typeof AREAS_PLAN)[number]

// Básico: de 1 a 3 hábitos durante 2 a 4 semanas. Regular: un programa con guía semanal.
export const NIVELES_PLAN = ['Basico', 'Regular'] as const
export type NivelPlan = (typeof NIVELES_PLAN)[number]

export const INTENSIDADES_PLAN = ['Baja', 'Media', 'Alta'] as const
export type IntensidadPlan = (typeof INTENSIDADES_PLAN)[number]

export const SEXOS_PLAN = ['Todos', 'Mujer', 'Hombre'] as const
export type SexoPlan = (typeof SEXOS_PLAN)[number]

// Momento de la jornada al que se ata una tarea; la hora concreta la calcula el servidor (Fase 4).
export const ANCLAS = [
  'AlDespertar',
  'AlLlegarAlTrabajo',
  'AMediaManana',
  'DespuesDelAlmuerzo',
  'AMediaTarde',
  'AlSalirDelTrabajo',
  'AntesDeCenar',
  'AntesDeDormir',
] as const
export type Ancla = (typeof ANCLAS)[number]

export const MOTIVOS_NO_APTO = [
  'FueraDeRangoDeEdad',
  'SexoDelPlan',
  'SinPlanesDePeso',
  'IntensidadNoRecomendada',
  'ImpactoNoRecomendado',
  'ZonaAEvitar',
  'Contraindicado',
  'ContieneAlergenos',
] as const
export type MotivoNoApto = (typeof MOTIVOS_NO_APTO)[number]

export interface FiltrosPlanes {
  area?: AreaPlan
  nivel?: NivelPlan
  perfilLaboral?: PerfilLaboral
}

// Tarjeta de un plan en el catálogo público.
export interface PlanResumenDto {
  slug: string
  nombre: string
  resumen: string
  area: AreaPlan
  nivel: NivelPlan
  intensidad: IntensidadPlan
  semanas: number
  sexo: SexoPlan
  edadMinima: number
  edadMaxima: number
  // Vacío: pensado para todas las jornadas.
  perfilesLaborales: PerfilLaboral[]
  // Siempre 3.
  beneficios: string[]
  // Empezarlo exige el cuestionario de salud y el consentimiento (Fase 4).
  requiereCuestionario: boolean
}

export interface TareaPlantillaDto {
  orden: number
  nombre: string
  instrucciones: string
  periodicidad: Periodicidad
  cantidad: number
  unidad: string
  // "Hoy solo lo mínimo": lo que cuenta en un mal día.
  versionMinima: string
  anclaSugerida: Ancla
}

// El tip extra de cada semana no viaja: se desbloquea al seguir el plan (Fase 4).
export interface SemanaGuiaDto {
  semana: number
  texto: string
}

export interface PlanDetalleDto extends PlanResumenDto {
  descripcion: string
  // Null: sirve en cualquier lugar.
  lugar: Lugar | null
  impacto: boolean
  zonasCorporales: ZonaCorporal[]
  contraindicaciones: CondicionSalud[]
  alergenos: Alergia[]
  avisoSeguridad: string
  tareas: TareaPlantillaDto[]
  // Una entrada por semana o ninguna (un Básico puede no llevarla).
  guiaSemanal: SemanaGuiaDto[]
}

export interface PlanRecomendadoDto {
  plan: PlanResumenDto
  motivos: string[]
  advertencias: string[]
}

export interface RazonNoAptoDto {
  motivo: MotivoNoApto
  mensaje: string
}

export interface PlanNoAptoDto {
  plan: PlanResumenDto
  razones: RazonNoAptoDto[]
}

export interface RecomendacionesDto {
  // Ordenados: la variante de "Mi día" de la jornada va primera.
  recomendados: PlanRecomendadoDto[]
  // Se muestran como "No recomendado para ti", con su razón.
  noAptos: PlanNoAptoDto[]
  // Falso: se recomendó por la jornada; falta el cuestionario o el consentimiento de salud.
  personalizadoConSalud: boolean
}

// Los slugs son estables y vienen del catálogo, pero también de la URL y del almacenamiento local:
// se validan por formato antes de usarlos (igual que el backend: minúsculas, números y guiones).
const FORMATO_SLUG = /^[a-z0-9]+(-[a-z0-9]+)*$/
export const LONGITUD_SLUG = 60

export function esSlugPlan(valor: unknown): valor is string {
  return typeof valor === 'string' && valor.length <= LONGITUD_SLUG && FORMATO_SLUG.test(valor)
}

// Variantes de "Mi día de trabajo saludable": la puerta de entrada de cada jornada (P2).
export const SLUGS_MI_DIA = [
  'mi-dia-oficina',
  'mi-dia-turno-nocturno',
  'mi-dia-trabajo-fisico',
] as const

export const esMiDia = (slug: string): boolean => (SLUGS_MI_DIA as readonly string[]).includes(slug)

export type { PerfilLaboral }
