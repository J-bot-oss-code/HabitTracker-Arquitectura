import type { Tono } from '@/shared/ui/Badge'
import { parsearFechaUtc } from '@/shared/lib/fechas'
import type { EstadoHabito, HabitoDto } from './types'

export const presentacionEstado: Record<EstadoHabito, { etiqueta: string; tono: Tono }> = {
  Planificado: { etiqueta: 'Planificado', tono: 'info' },
  Activo: { etiqueta: 'Activo', tono: 'exito' },
  Pausado: { etiqueta: 'Pausado', tono: 'aviso' },
  Completado: { etiqueta: 'Completado', tono: 'marca' },
}

export interface AccionTransicion {
  destino: EstadoHabito
  etiqueta: string
  habilitada: boolean
  motivo?: string
}

// Solo decide qué botones ofrecer. La regla vive en un único punto del backend
// (TransicionesHabito.EsTransicionValida, RD-04) y el servidor la vuelve a aplicar siempre:
// si este mapa se desactualiza, la API rechaza la transición y la interfaz muestra el error.
export function accionesDisponibles(habito: HabitoDto): AccionTransicion[] {
  switch (habito.estado) {
    case 'Planificado':
      return [{ destino: 'Activo', etiqueta: 'Activar', habilitada: true }]
    case 'Activo':
      return [
        { destino: 'Pausado', etiqueta: 'Pausar', habilitada: true },
        {
          destino: 'Completado',
          etiqueta: 'Completar',
          habilitada: habito.cumpleMetaFinal,
          motivo: habito.cumpleMetaFinal
            ? undefined
            : 'Alcanza la meta del período para poder completarlo.',
        },
      ]
    case 'Pausado':
      return [{ destino: 'Activo', etiqueta: 'Reanudar', habilitada: true }]
    case 'Completado':
      return []
  }
}

function inicioSemanaIsoUtc(ahora: Date): Date {
  const inicio = new Date(Date.UTC(ahora.getUTCFullYear(), ahora.getUTCMonth(), ahora.getUTCDate()))
  const diasDesdeLunes = (inicio.getUTCDay() + 6) % 7
  inicio.setUTCDate(inicio.getUTCDate() - diasDesdeLunes)
  return inicio
}

// Avance acumulado del período actual (hoy o semana ISO), solo para la barra de progreso.
// El dato que decide si se puede completar es cumpleMetaFinal, calculado por el servidor.
export function avanceDelPeriodo(habito: HabitoDto, ahora = new Date()): number {
  const inicioCiclo = parsearFechaUtc(habito.fechaInicioCiclo)
  const desde =
    habito.periodicidad === 'Diaria'
      ? new Date(Date.UTC(ahora.getUTCFullYear(), ahora.getUTCMonth(), ahora.getUTCDate()))
      : inicioSemanaIsoUtc(ahora)
  const hasta = new Date(desde)
  hasta.setUTCDate(hasta.getUTCDate() + (habito.periodicidad === 'Diaria' ? 1 : 7))

  return habito.registros
    .map((r) => ({ ...r, fecha: parsearFechaUtc(r.fecha) }))
    .filter((r) => r.fecha >= inicioCiclo && r.fecha >= desde && r.fecha < hasta)
    .reduce((total, r) => total + r.cantidad, 0)
}

export const etiquetaPeriodo = { Diaria: 'hoy', Semanal: 'esta semana' } as const
