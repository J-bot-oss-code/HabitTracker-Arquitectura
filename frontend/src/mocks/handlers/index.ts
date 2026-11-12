import { adminUsersHandlers } from './adminUsers'
import { authHandlers } from './auth'
import { habitsHandlers } from './habits'
import { planesHandlers } from './planes'
import { perfilHandlers } from './perfil'
import { permissionsHandlers } from './permissions'
import { privacidadHandlers } from './privacidad'
import { progresoHandlers } from './progreso'
import { propuestasHandlers } from './propuestas'

export const handlers = [
  ...authHandlers,
  ...habitsHandlers,
  // Perfil, cuestionario de salud y progreso corporal (HabitTracker v2, Fase 2).
  ...perfilHandlers,
  ...progresoHandlers,
  // Catálogo de planes y recomendador (HabitTracker v2, Fase 3).
  ...planesHandlers,
  ...permissionsHandlers,
  ...adminUsersHandlers,
  // Centro de privacidad real (consentimientos, baja de cuenta y descarga de datos).
  ...privacidadHandlers,
  // Endpoints sin backend todavía (ver propuestas/index.ts).
  ...propuestasHandlers,
]
