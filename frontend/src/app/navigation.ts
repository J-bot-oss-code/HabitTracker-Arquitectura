import type { Rol } from '@/shared/auth/roles'
import type { NombreIcono } from '@/shared/ui/Icono'

export interface ItemNavegacion {
  a: string
  etiqueta: string
  icono: NombreIcono
  roles: Rol[]
  proximamente?: boolean
  // Solo coincide con la ruta exacta (/admin no debe marcarse activa en /admin/usuarios).
  fin?: boolean
}

// El menú se adapta al rol para no ofrecer lo que el servidor va a rechazar
// (p. ej. api/habits es solo para Estándar). Ocultar no protege: eso lo hace la API (RD-06).
export const NAVEGACION: ItemNavegacion[] = [
  { a: '/hoy', etiqueta: 'Hoy', icono: 'rayo', roles: ['Estandar'] },
  { a: '/habitos', etiqueta: 'Mis hábitos', icono: 'habitos', roles: ['Estandar'] },
  { a: '/progreso', etiqueta: 'Progreso', icono: 'trofeo', roles: ['Estandar'] },
  { a: '/permisos', etiqueta: 'Mis permisos', icono: 'permisos', roles: ['Estandar'] },
  { a: '/admin', etiqueta: 'Resumen', icono: 'reportes', roles: ['Administrador'], fin: true },
  { a: '/admin/permisos', etiqueta: 'Solicitudes', icono: 'permisos', roles: ['Administrador'] },
  { a: '/admin/usuarios', etiqueta: 'Usuarios', icono: 'usuarios', roles: ['Administrador'] },
  { a: '/admin/auditoria', etiqueta: 'Auditoría', icono: 'auditoria', roles: ['Administrador'] },
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
  // Cuenta, apariencia, inicio de sesión, seguridad y privacidad viven juntas en Configuración (siempre al final).
  {
    a: '/ajustes',
    etiqueta: 'Configuración',
    icono: 'ajustes',
    roles: ['Estandar', 'Administrador'],
  },
]

// Secciones de Configuración (app/ajustes/AjustesLayout). Privacidad y datos es solo del Estándar:
// el Administrador recibe 403 en /api/privacy (y la ruta lo comprueba con RequireAuth).
export interface SeccionAjustes {
  a: string
  etiqueta: string
  icono: NombreIcono
  roles: Rol[]
  // Texto de la tarjeta de acceso rápido (Configuración › Cuenta, en móvil).
  descripcion: string
}

export const SECCIONES_AJUSTES: SeccionAjustes[] = [
  {
    a: '/ajustes/cuenta',
    etiqueta: 'Cuenta',
    icono: 'cuenta',
    roles: ['Estandar', 'Administrador'],
    descripcion: 'Tu correo y tu rol.',
  },
  {
    a: '/ajustes/seguridad',
    etiqueta: 'Contraseña y seguridad',
    icono: 'candado',
    roles: ['Estandar', 'Administrador'],
    descripcion: 'Cambia tu contraseña o crea una si entras con Google.',
  },
  {
    a: '/ajustes/inicio-sesion',
    etiqueta: 'Inicio de sesión',
    icono: 'dispositivos',
    roles: ['Estandar', 'Administrador'],
    descripcion: 'Google vinculado y dispositivos con la sesión abierta.',
  },
  {
    a: '/ajustes/privacidad',
    etiqueta: 'Privacidad y datos',
    icono: 'permisos',
    roles: ['Estandar'],
    descripcion: 'Descarga tus datos, revisa los consentimientos o retírate.',
  },
  {
    a: '/ajustes/apariencia',
    etiqueta: 'Apariencia',
    icono: 'tema',
    roles: ['Estandar', 'Administrador'],
    descripcion: 'Tema claro, oscuro o el de tu dispositivo.',
  },
]

export function seccionesAjustesPara(rol: Rol) {
  return SECCIONES_AJUSTES.filter((s) => s.roles.includes(rol))
}

export function navegacionPara(rol: Rol): ItemNavegacion[] {
  return NAVEGACION.filter((item) => item.roles.includes(rol))
}

export function rutaInicial(rol: Rol): string {
  return rol === 'Administrador' ? '/admin' : '/hoy'
}

// Rutas reservadas a un rol. Debe coincidir con los <RequireAuth roles> de routes.tsx.
const RUTAS_POR_ROL: Array<{ patron: RegExp; roles: Rol[] }> = [
  { patron: /^\/admin(\/|$)/i, roles: ['Administrador'] },
  { patron: /^\/hoy\/?$/i, roles: ['Estandar'] },
  { patron: /^\/progreso\/?$/i, roles: ['Estandar'] },
  { patron: /^\/ajustes\/privacidad\/?$/i, roles: ['Estandar'] },
  { patron: /^\/habitos(\/|$)/i, roles: ['Estandar'] },
  { patron: /^\/bienvenida\/?$/i, roles: ['Estandar'] },
  { patron: /^\/permisos\/?$/i, roles: ['Estandar'] },
]

// Indica si el rol puede abrir la ruta. Sirve para no devolver a alguien, tras iniciar sesión,
// a la página que dejó abierta un usuario de otro rol.
export function rutaPermitida(rol: Rol, ruta: string): boolean {
  const camino = ruta.split(/[?#]/)[0]
  const regla = RUTAS_POR_ROL.find((r) => r.patron.test(camino))
  return !regla || regla.roles.includes(rol)
}
