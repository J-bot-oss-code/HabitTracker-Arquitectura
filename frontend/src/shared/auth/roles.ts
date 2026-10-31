// Roles del backend (AccessControl.Domain.Rol). Viajan como texto en el JWT y en los DTOs.
export const ROLES = ['Estandar', 'Administrador'] as const
export type Rol = (typeof ROLES)[number]

export const etiquetaRol: Record<Rol, string> = {
  Estandar: 'Estándar',
  Administrador: 'Administrador',
}

export function esRol(valor: unknown): valor is Rol {
  return typeof valor === 'string' && (ROLES as readonly string[]).includes(valor)
}
