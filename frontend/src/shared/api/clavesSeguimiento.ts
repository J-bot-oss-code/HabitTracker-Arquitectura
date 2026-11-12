import type { QueryClient } from '@tanstack/react-query'

// Raíces de las consultas del seguimiento diario. Cambian juntas: un avance o el cambio de estado de un
// hábito mueven Hoy, el progreso de Mis planes y el resumen de constancia, y un plan que se pausa o
// termina mueve sus hábitos. Viven aquí, y no en el api.ts de una feature, para que `habits` y
// `mis-planes` se invaliden entre sí sin importarse en círculo.
export const CLAVES_SEGUIMIENTO = {
  habitos: ['habits'],
  misPlanes: ['mis-planes'],
  hoy: ['hoy'],
  resumen: ['resumen-progreso'],
} as const

export function invalidarSeguimiento(queryClient: QueryClient): Promise<void[]> {
  return Promise.all(
    Object.values(CLAVES_SEGUIMIENTO).map((queryKey) =>
      queryClient.invalidateQueries({ queryKey }),
    ),
  )
}
