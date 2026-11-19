import { PREFIJO_ELIMINAR_ARCHIVO } from '@/features/documentos/types'
import type { Tono } from '@/shared/ui/Badge'
import type { EstadoSolicitudPermiso } from './types'

// Permisos que hoy protege el backend. Al agregar una acción protegida nueva, se registra aquí.
export const CATALOGO_PERMISOS = [
  {
    clave: 'Habits.AdvancedReconfig',
    nombre: 'Reconfiguración avanzada de hábitos',
    descripcion:
      'Cambiar periodicidad, cantidad objetivo y unidad de un hábito. Inicia un ciclo nuevo y conserva el historial.',
  },
] as const

// El mismo gesto dice lo mismo en toda la app.
export const MENSAJE_PEDIDO_ENVIADO = 'Listo: pediste el permiso. Un Administrador lo revisará.'

export const NOMBRE_ELIMINAR_ARCHIVO = 'Eliminar un archivo de un plan en curso'

// Documents.DeleteLinked.{id sin guiones}: no está en el catálogo porque hay una clave por documento.
const ID_SIN_GUIONES = /^[0-9a-f]{32}$/i

export const esClaveEliminarArchivo = (clave: string): boolean =>
  clave.startsWith(PREFIJO_ELIMINAR_ARCHIVO) &&
  ID_SIN_GUIONES.test(clave.slice(PREFIJO_ELIMINAR_ARCHIVO.length))

// El id del documento (con guiones) al que se refiere una clave Documents.DeleteLinked.*, o null si la clave es otra.
export function documentoDeClave(clave: string): string | null {
  if (!esClaveEliminarArchivo(clave)) return null
  const n = clave.slice(PREFIJO_ELIMINAR_ARCHIVO.length).toLowerCase()
  return `${n.slice(0, 8)}-${n.slice(8, 12)}-${n.slice(12, 16)}-${n.slice(16, 20)}-${n.slice(20)}`
}

export function nombrePermiso(clave: string): string {
  if (esClaveEliminarArchivo(clave)) return NOMBRE_ELIMINAR_ARCHIVO
  return CATALOGO_PERMISOS.find((p) => p.clave === clave)?.nombre ?? clave
}

export const presentacionSolicitud: Record<
  EstadoSolicitudPermiso,
  { etiqueta: string; tono: Tono }
> = {
  Pendiente: { etiqueta: 'Pendiente', tono: 'aviso' },
  Aprobada: { etiqueta: 'Aprobada', tono: 'info' },
  Aplicada: { etiqueta: 'Aplicada', tono: 'exito' },
  Rechazada: { etiqueta: 'Rechazada', tono: 'peligro' },
}
