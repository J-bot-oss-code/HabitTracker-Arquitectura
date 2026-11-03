// Catálogo público de metas (especificación §5.1, "Elige una meta"). Es contenido estático:
// se puede explorar sin cuenta. Cuando el backend tenga plantillas de plan (`PlanTemplate`,
// docs/mejoras §4.1), este catálogo vendrá de la API; el `slug` es el identificador estable
// que viaja en /registro?meta=<slug>.
export const AREAS = ['Fisico', 'Nutricion', 'Descanso', 'Mente'] as const
export type Area = (typeof AREAS)[number]

export const etiquetaArea: Record<Area, string> = {
  Fisico: 'Físico',
  Nutricion: 'Nutrición',
  Descanso: 'Descanso',
  Mente: 'Mente',
}

export type Nivel = 'Principiante' | 'Intermedio' | 'Todos'

export interface MetaCatalogo {
  slug: string
  nombre: string
  area: Area
  nivel: Nivel
  descripcion: string
  // Duración total del plan en semanas (21 días = 3 semanas) y su texto para la tarjeta.
  semanas: number
  duracion: string
  frecuencia: string
  // Resumen y beneficios del panel de tinta en el registro (§5.2).
  resumen: string
  beneficios: [string, string, string]
}

export const METAS_CATALOGO = [
  {
    slug: 'primeros-5-km',
    nombre: 'Primeros 5 km',
    area: 'Fisico',
    nivel: 'Principiante',
    descripcion: 'Pasa de caminar a correr 5 km seguidos combinando trote suave y caminata.',
    semanas: 8,
    duracion: '8 semanas',
    frecuencia: '3 días por semana',
    resumen: '8 semanas · 3 salidas por semana · nivel principiante. Sin experiencia previa.',
    beneficios: [
      'Sesiones de 25 a 35 minutos con calentamiento incluido',
      'La carga sube poco a poco para evitar lesiones',
      'Tu racha y tu avance semanal siempre a la vista',
    ],
  },
  {
    slug: 'ganar-masa-muscular',
    nombre: 'Ganar masa muscular',
    area: 'Fisico',
    nivel: 'Intermedio',
    descripcion:
      'Rutina de fuerza por grupos musculares con progresión de cargas y proteína diaria.',
    semanas: 12,
    duracion: '12 semanas',
    frecuencia: '4 días por semana',
    resumen: '12 semanas · 4 entrenamientos por semana · nivel intermedio.',
    beneficios: [
      'Rutinas por grupo muscular con series y descansos',
      'Registro de cargas para ver tus récords',
      'Hábito diario de proteína e hidratación',
    ],
  },
  {
    slug: 'perder-grasa-sostenible',
    nombre: 'Perder grasa de forma sostenible',
    area: 'Nutricion',
    nivel: 'Todos',
    descripcion: 'Pequeños hábitos de alimentación, agua y pasos, sin dietas extremas.',
    semanas: 12,
    duracion: '12 semanas',
    frecuencia: 'diario',
    resumen: '12 semanas · hábitos diarios · para cualquier nivel. Ritmo saludable y medible.',
    beneficios: [
      'Objetivo de calorías estimado, nunca extremo',
      'Hábitos de agua, pasos y comidas que se marcan en segundos',
      'Peso y medidas en una gráfica que sí puedes leer',
    ],
  },
  {
    slug: 'fuerza-en-casa',
    nombre: 'Fuerza en casa',
    area: 'Fisico',
    nivel: 'Principiante',
    descripcion: 'Ejercicios con tu propio peso, sin equipo, en sesiones cortas.',
    semanas: 6,
    duracion: '6 semanas',
    frecuencia: '3 días por semana',
    resumen: '6 semanas · 3 sesiones por semana · nivel principiante. Sin equipo.',
    beneficios: [
      'Sesiones de 20 minutos con tu propio peso',
      'Técnica explicada paso a paso',
      'Progresión semanal clara',
    ],
  },
  {
    slug: 'dormir-mejor',
    nombre: 'Dormir mejor',
    area: 'Descanso',
    nivel: 'Todos',
    descripcion: 'Una rutina de sueño constante: horario fijo, menos pantallas y descanso real.',
    semanas: 4,
    duracion: '4 semanas',
    frecuencia: 'diario',
    resumen: '4 semanas · hábitos diarios · para cualquier nivel.',
    beneficios: [
      'Horario de sueño con recordatorio',
      'Hábitos de desconexión antes de dormir',
      'Seguimiento de horas dormidas',
    ],
  },
  {
    slug: 'reducir-estres',
    nombre: 'Reducir el estrés',
    area: 'Mente',
    nivel: 'Todos',
    descripcion: 'Respiración, pausas activas y escritura breve para bajar el ritmo cada día.',
    semanas: 3,
    duracion: '21 días',
    frecuencia: 'diario',
    resumen: '21 días · prácticas diarias de 10 minutos · para cualquier nivel.',
    beneficios: [
      'Prácticas guiadas de 5 a 10 minutos',
      'Pausas activas durante la jornada',
      'Registro breve de cómo te sientes',
    ],
  },
] as const satisfies readonly MetaCatalogo[]

export type SlugMeta = (typeof METAS_CATALOGO)[number]['slug']

// El slug llega de la URL: se valida contra el catálogo y un valor desconocido devuelve null.
export function buscarMeta(slug: string | null | undefined): MetaCatalogo | null {
  if (!slug) return null
  return METAS_CATALOGO.find((m) => m.slug === slug) ?? null
}

export function metasDelArea(area: Area | 'todas'): MetaCatalogo[] {
  return area === 'todas' ? [...METAS_CATALOGO] : METAS_CATALOGO.filter((m) => m.area === area)
}
