// Datos corporales compartidos por la calculadora pública y el onboarding. Son la parte de
// esta feature que otras pueden importar (frontend/AGENTS.md: una feature solo usa de otra su
// types.ts/api.ts).
export const SEXOS = ['mujer', 'hombre'] as const
export type Sexo = (typeof SEXOS)[number]

export const etiquetaSexo: Record<Sexo, string> = { mujer: 'Mujer', hombre: 'Hombre' }

export const NIVELES_ACTIVIDAD = ['sedentaria', 'ligera', 'moderada', 'alta', 'muyAlta'] as const
export type NivelActividad = (typeof NIVELES_ACTIVIDAD)[number]

export const infoActividad: Record<NivelActividad, { etiqueta: string; factor: number }> = {
  sedentaria: { etiqueta: 'Sedentaria (poco o nada de ejercicio)', factor: 1.2 },
  ligera: { etiqueta: 'Ligera (1–3 días por semana)', factor: 1.375 },
  moderada: { etiqueta: 'Moderada (3–5 días por semana)', factor: 1.55 },
  alta: { etiqueta: 'Alta (6–7 días por semana)', factor: 1.725 },
  muyAlta: { etiqueta: 'Muy alta (trabajo físico o doble sesión)', factor: 1.9 },
}

// Límite propuesto para el backend (mejora futura): el perfil físico aún no existe en
// Host/WebApi. La edad mínima sigue la de los términos de uso (docs/mejoras §7.2).
export const LIMITES_CORPORALES = {
  edad: { min: 16, max: 100 },
  alturaCm: { min: 120, max: 230 },
  pesoKg: { min: 30, max: 300 },
} as const
