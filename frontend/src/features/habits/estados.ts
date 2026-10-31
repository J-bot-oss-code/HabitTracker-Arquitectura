import type { Tono } from '@/shared/ui/Badge'
import { etiquetaDiaSemanaUtc, parsearFechaUtc } from '@/shared/lib/fechas'
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

// --- Métricas de presentación (rendimiento) ---
// No deciden nada de negocio: la meta final y las transiciones las valida el servidor.

const DIA_MS = 86_400_000

const indiceDia = (t: number) => Math.floor(t / DIA_MS)
// El 1-ene-1970 fue jueves; desplazando 3 días las semanas empiezan en lunes (ISO).
const indiceSemana = (dia: number) => Math.floor((dia + 3) / 7)

function sumasPorPeriodo(habito: HabitoDto): Map<number, number> {
  const inicioCiclo = parsearFechaUtc(habito.fechaInicioCiclo).getTime()
  const sumas = new Map<number, number>()
  for (const r of habito.registros) {
    const t = parsearFechaUtc(r.fecha).getTime()
    if (t < inicioCiclo) continue
    const dia = indiceDia(t)
    const clave = habito.periodicidad === 'Diaria' ? dia : indiceSemana(dia)
    sumas.set(clave, (sumas.get(clave) ?? 0) + r.cantidad)
  }
  return sumas
}

// Períodos consecutivos (días o semanas) que alcanzaron el objetivo. El período en curso
// no rompe la racha mientras siga abierto: si aún no llega a la meta, se cuenta desde el anterior.
export function rachaActual(habito: HabitoDto, ahora = new Date()): number {
  const sumas = sumasPorPeriodo(habito)
  const hoy = indiceDia(ahora.getTime())
  const actual = habito.periodicidad === 'Diaria' ? hoy : indiceSemana(hoy)
  const cumple = (p: number) => (sumas.get(p) ?? 0) >= habito.cantidadObjetivo

  let periodo = cumple(actual) ? actual : actual - 1
  let racha = 0
  while (cumple(periodo)) {
    racha += 1
    periodo -= 1
  }
  return racha
}

export interface AvanceDia {
  etiqueta: string
  valor: number
}

// Total registrado en cada uno de los últimos `dias` días UTC (el último es hoy).
export function avancePorDia(habito: HabitoDto, dias = 7, ahora = new Date()): AvanceDia[] {
  const inicioCiclo = parsearFechaUtc(habito.fechaInicioCiclo).getTime()
  const hoy = indiceDia(ahora.getTime())
  const porDia = new Map<number, number>()
  for (const r of habito.registros) {
    const t = parsearFechaUtc(r.fecha).getTime()
    if (t < inicioCiclo) continue
    porDia.set(indiceDia(t), (porDia.get(indiceDia(t)) ?? 0) + r.cantidad)
  }
  return Array.from({ length: dias }, (_, i) => {
    const dia = hoy - (dias - 1 - i)
    return {
      etiqueta: etiquetaDiaSemanaUtc(new Date(dia * DIA_MS)),
      valor: porDia.get(dia) ?? 0,
    }
  })
}

export function totalDelCiclo(habito: HabitoDto): number {
  const inicioCiclo = parsearFechaUtc(habito.fechaInicioCiclo).getTime()
  return habito.registros
    .filter((r) => parsearFechaUtc(r.fecha).getTime() >= inicioCiclo)
    .reduce((total, r) => total + r.cantidad, 0)
}

export const iconoEstado = {
  Planificado: 'calendario',
  Activo: 'rayo',
  Pausado: 'pausa',
  Completado: 'trofeo',
} as const satisfies Record<EstadoHabito, string>
