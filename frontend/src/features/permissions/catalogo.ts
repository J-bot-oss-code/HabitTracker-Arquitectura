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

export function nombrePermiso(clave: string): string {
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
