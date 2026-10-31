import type { Rol } from '@/shared/auth/roles'

export interface ItemNavegacion {
  a: string
  etiqueta: string
  roles: Rol[]
  proximamente?: boolean
}

// El menú se adapta al rol para no ofrecer lo que el servidor va a rechazar
// (p. ej. api/habits es solo para Estándar). Ocultar no protege: eso lo hace la API (RD-06).
export const NAVEGACION: ItemNavegacion[] = [
  { a: '/habitos', etiqueta: 'Mis hábitos', roles: ['Estandar'] },
  { a: '/permisos', etiqueta: 'Mis permisos', roles: ['Estandar'] },
  { a: '/admin/permisos', etiqueta: 'Solicitudes', roles: ['Administrador'] },
  { a: '/admin/usuarios', etiqueta: 'Usuarios', roles: ['Administrador'] },
  {
    a: '/documentos',
    etiqueta: 'Documentos',
    roles: ['Estandar', 'Administrador'],
    proximamente: true,
  },
  {
    a: '/notificaciones',
    etiqueta: 'Notificaciones',
    roles: ['Estandar', 'Administrador'],
    proximamente: true,
  },
  {
    a: '/reportes',
    etiqueta: 'Reportes',
    roles: ['Estandar', 'Administrador'],
    proximamente: true,
  },
  {
    a: '/admin/cola-correos',
    etiqueta: 'Cola de correos',
    roles: ['Administrador'],
    proximamente: true,
  },
  { a: '/admin/auditoria', etiqueta: 'Auditoría', roles: ['Administrador'], proximamente: true },
]

export function navegacionPara(rol: Rol): ItemNavegacion[] {
  return NAVEGACION.filter((item) => item.roles.includes(rol))
}

export function rutaInicial(rol: Rol): string {
  return rol === 'Administrador' ? '/admin/permisos' : '/habitos'
}
