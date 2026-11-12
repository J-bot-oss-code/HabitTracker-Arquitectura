import { useQuery } from '@tanstack/react-query'
import { api } from '@/shared/api/http'
import { esErrorHttp } from '@/shared/api/problem'
import type { FiltrosPlanes, PlanDetalleDto, PlanResumenDto, RecomendacionesDto } from './types'
import { esSlugPlan } from './types'

// Superficie de los planes para otras features (frontend/AGENTS.md: una feature solo usa de otra su
// api.ts/types.ts). Los textos en español de cada enum viajan con ella.
export {
  DESCRIPCION_NIVEL,
  duracionPlan,
  ETIQUETA_ANCLA,
  ETIQUETA_AREA,
  ETIQUETA_INTENSIDAD,
  ETIQUETA_NIVEL,
  ETIQUETA_PERIODICIDAD,
  ETIQUETA_SEXO_PLAN,
  etiquetaNivelConDuracion,
} from './etiquetas'
export {
  AREAS_PLAN,
  esMiDia,
  esSlugPlan,
  NIVELES_PLAN,
  SLUGS_MI_DIA,
  type AreaPlan,
  type FiltrosPlanes,
  type NivelPlan,
  type PlanDetalleDto,
  type PlanNoAptoDto,
  type PlanRecomendadoDto,
  type PlanResumenDto,
  type RecomendacionesDto,
} from './types'

// PlansController: el catálogo y el detalle son anónimos (la landing los usa sin sesión).
export const planesApi = {
  listar: (filtros: FiltrosPlanes = {}) => {
    const consulta = new URLSearchParams()
    if (filtros.area) consulta.set('area', filtros.area)
    if (filtros.nivel) consulta.set('nivel', filtros.nivel)
    if (filtros.perfilLaboral) consulta.set('perfilLaboral', filtros.perfilLaboral)
    const texto = consulta.toString()
    return api.get<PlanResumenDto[]>(texto ? `/api/plans?${texto}` : '/api/plans')
  },
  obtener: (slug: string) => api.get<PlanDetalleDto>(`/api/plans/${encodeURIComponent(slug)}`),
  recomendados: () => api.get<RecomendacionesDto>('/api/plans/recommended'),
}

export const planesKeys = {
  todos: ['planes'] as const,
  lista: (filtros: FiltrosPlanes = {}) => ['planes', 'lista', filtros] as const,
  detalle: (slug: string) => ['planes', 'detalle', slug] as const,
  recomendados: ['planes', 'recomendados'] as const,
}

// El servidor guarda el catálogo 5 minutos en caché pública: no hace falta pedirlo más seguido.
const CINCO_MINUTOS = 5 * 60_000

export function usePlanes(filtros: FiltrosPlanes = {}) {
  return useQuery({
    queryKey: planesKeys.lista(filtros),
    queryFn: () => planesApi.listar(filtros),
    staleTime: CINCO_MINUTOS,
  })
}

export function usePlan(slug: string | undefined) {
  return useQuery({
    queryKey: planesKeys.detalle(slug ?? ''),
    queryFn: () => planesApi.obtener(slug ?? ''),
    // Un slug que no tiene el formato del catálogo ni se pide: la página muestra "no encontrado".
    enabled: esSlugPlan(slug),
    staleTime: CINCO_MINUTOS,
    // 404 plan_no_encontrado es una respuesta, no un fallo de red: no se repite.
    retry: (intentos, error) => !esErrorHttp(error, 404) && intentos < 2,
  })
}

// Un plan del catálogo por su slug, para quien solo necesita su nombre o su duración (Hoy, Progreso,
// el panel del registro). Comparte la consulta de la lista completa, así que no pide nada extra.
export function usePlanResumen(slug: string | null | undefined) {
  const planes = usePlanes()
  const plan = esSlugPlan(slug) ? planes.data?.find((p) => p.slug === slug) : undefined
  return { plan, cargando: planes.isPending }
}

// Recomendados para la persona. Solo con sesión de Estándar y perfil (404 perfil_no_encontrado antes).
export function useRecomendados(habilitado = true) {
  return useQuery({
    queryKey: planesKeys.recomendados,
    queryFn: planesApi.recomendados,
    enabled: habilitado,
    // Depende del perfil y del cuestionario: se vuelve a pedir al volver a la pantalla.
    staleTime: 0,
    retryOnMount: false,
  })
}
