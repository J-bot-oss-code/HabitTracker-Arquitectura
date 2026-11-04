import { adminUsersHandlers } from './adminUsers'
import { authHandlers } from './auth'
import { habitsHandlers } from './habits'
import { permissionsHandlers } from './permissions'
import { propuestasHandlers } from './propuestas'

export const handlers = [
  ...authHandlers,
  ...habitsHandlers,
  ...permissionsHandlers,
  ...adminUsersHandlers,
  // Endpoints sin backend todavía (ver propuestas/index.ts).
  ...propuestasHandlers,
]
