import type { Rol } from '@/shared/auth/roles'
import type { NombreIcono } from '@/shared/ui/Icono'

export interface ItemNavegacion {
  a: string
  etiqueta: string
  icono: NombreIcono
  roles: Rol[]
  proximamente?: boolean
}

// El menú se adapta al rol para no ofrecer lo que el servidor va a rechazar
// (p. ej. api/habits es solo para Estándar). Ocultar no protege: eso lo hace la API (RD-06).
export const NAVEGACION: ItemNavegacion[] = [
  { a: '/habitos', etiqueta: 'Mis hábitos', icono: 'habitos', roles: ['Estandar'] },
  { a: '/permisos', etiqueta: 'Mis permisos', icono: 'permisos', roles: ['Estandar'] },
  { a: '/admin/permisos', etiqueta: 'Solicitudes', icono: 'permisos', roles: ['Administrador'] },
  { a: '/admin/usuarios', etiqueta: 'Usuarios', icono: 'usuarios', roles: ['Administrador'] },
  {
    a: '/documentos',
    etiqueta: 'Documentos',
    icono: 'documentos',
    roles: ['Estandar', 'Administrador'],
    proximamente: true,
  },
  {
    a: '/notificaciones',
    etiqueta: 'Notificaciones',
    icono: 'notificaciones',
    roles: ['Estandar', 'Administrador'],
    proximamente: true,
  },
  {
    a: '/reportes',
    etiqueta: 'Reportes',
    icono: 'reportes',
    roles: ['Estandar', 'Administrador'],
    proximamente: true,
  },
  {
    a: '/admin/cola-correos',
    etiqueta: 'Cola de correos',
    icono: 'correo',
    roles: ['Administrador'],
    proximamente: true,
  },
  {
    a: '/admin/auditoria',
    etiqueta: 'Auditoría',
    icono: 'auditoria',
    roles: ['Administrador'],
    proximamente: true,
  },
]

export function navegacionPara(rol: Rol): ItemNavegacion[] {
  return NAVEGACION.filter((item) => item.roles.includes(rol))
}

export function rutaInicial(rol: Rol): string {
  return rol === 'Administrador' ? '/admin/permisos' : '/habitos'
}

// Rutas reservadas a un rol. Debe coincidir con los <RequireAuth roles> de routes.tsx.
const RUTAS_POR_ROL: Array<{ patron: RegExp; roles: Rol[] }> = [
  { patron: /^\/admin(\/|$)/i, roles: ['Administrador'] },
  { patron: /^\/habitos(\/|$)/i, roles: ['Estandar'] },
  { patron: /^\/permisos\/?$/i, roles: ['Estandar'] },
]

// Indica si el rol puede abrir la ruta. Sirve para no devolver a alguien, tras iniciar sesión,
// a la página que dejó abierta un usuario de otro rol.
export function rutaPermitida(rol: Rol, ruta: string): boolean {
  const camino = ruta.split(/[?#]/)[0]
  const regla = RUTAS_POR_ROL.find((r) => r.patron.test(camino))
  return !regla || regla.roles.includes(rol)
}
