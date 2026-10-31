import { adminUsersHandlers } from './adminUsers'
import { authHandlers } from './auth'
import { habitsHandlers } from './habits'
import { permissionsHandlers } from './permissions'

export const handlers = [
  ...authHandlers,
  ...habitsHandlers,
  ...permissionsHandlers,
  ...adminUsersHandlers,
]
