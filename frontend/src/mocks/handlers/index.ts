import { adminUsersHandlers } from './adminUsers'
import { authHandlers } from './auth'
import { habitsHandlers } from './habits'
import { permissionsHandlers } from './permissions'
import { privacidadHandlers } from './privacidad'
import { propuestasHandlers } from './propuestas'

export const handlers = [
  ...authHandlers,
  ...habitsHandlers,
  ...permissionsHandlers,
  ...adminUsersHandlers,
  // Centro de privacidad real (consentimientos, baja de cuenta y descarga de datos).
  ...privacidadHandlers,
  // Endpoints sin backend todavía (ver propuestas/index.ts).
  ...propuestasHandlers,
]
