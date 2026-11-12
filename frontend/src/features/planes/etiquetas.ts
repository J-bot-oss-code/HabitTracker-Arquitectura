import type { Periodicidad } from '@/features/habits/types'
import type { Ancla, AreaPlan, IntensidadPlan, NivelPlan, PlanResumenDto, SexoPlan } from './types'

export const ETIQUETA_AREA: Record<AreaPlan, string> = {
  ControlDePeso: 'Control de peso',
  Gimnasio: 'Gimnasio',
  Estres: 'Estrés',
  Concentracion: 'Concentración',
  Descanso: 'Descanso',
  Hidratacion: 'Hidratación',
  Nutricion: 'Nutrición',
  Movilidad: 'Movilidad',
  Energia: 'Energía',
}

export const ETIQUETA_NIVEL: Record<NivelPlan, string> = {
  Basico: 'Básico',
  Regular: 'Regular',
}

export const ETIQUETA_NIVEL_PLURAL: Record<NivelPlan, string> = {
  Basico: 'Básicos',
  Regular: 'Regulares',
}

// Lo que significa cada nivel, para que la persona sepa qué le piden antes de abrir el plan.
export const DESCRIPCION_NIVEL: Record<NivelPlan, string> = {
  Basico: 'Pocos hábitos simples, de 2 a 4 semanas.',
  Regular: 'Un programa con una guía para cada semana.',
}

export const ETIQUETA_INTENSIDAD: Record<IntensidadPlan, string> = {
  Baja: 'Intensidad baja',
  Media: 'Intensidad media',
  Alta: 'Intensidad alta',
}

export const ETIQUETA_SEXO_PLAN: Record<SexoPlan, string> = {
  Todos: 'Para todas las personas',
  Mujer: 'Pensado para mujeres',
  Hombre: 'Pensado para hombres',
}

export const ETIQUETA_ANCLA: Record<Ancla, string> = {
  AlDespertar: 'Al despertar',
  AlLlegarAlTrabajo: 'Al llegar al trabajo',
  AMediaManana: 'A media mañana',
  DespuesDelAlmuerzo: 'Después del almuerzo',
  AMediaTarde: 'A media tarde',
  AlSalirDelTrabajo: 'Al salir del trabajo',
  AntesDeCenar: 'Antes de cenar',
  AntesDeDormir: 'Antes de dormir',
}

export const ETIQUETA_PERIODICIDAD: Record<Periodicidad, string> = {
  Diaria: 'cada día',
  Semanal: 'cada semana',
}

// El plan dura una cantidad entera de semanas ("Reducir el estrés" son 3, los 21 días de antes).
export const duracionPlan = (semanas: number): string =>
  `${semanas} ${semanas === 1 ? 'semana' : 'semanas'}`

export const etiquetaNivelConDuracion = (plan: Pick<PlanResumenDto, 'nivel' | 'semanas'>): string =>
  `${ETIQUETA_NIVEL[plan.nivel]} · ${duracionPlan(plan.semanas)}`
