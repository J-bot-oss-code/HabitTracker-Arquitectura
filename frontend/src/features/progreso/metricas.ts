import type { PuntoGrafica } from '@/shared/ui/GraficaLinea'
import { parsearFechaUtc } from '@/shared/lib/fechas'
import { ZONAS, type MedicionDto, type PesoDto, type Periodo, type Zona } from './types'

// Cálculos de la pantalla Progreso. Son de presentación: el servidor guarda los datos y no
// decide nada a partir de ellos. Las fechas son instantes UTC (RD-11).

const DIA_MS = 86_400_000

const DIAS_DEL_PERIODO: Record<Exclude<Periodo, 'todo'>, number> = {
  '4semanas': 28,
  '3meses': 91,
}

export const etiquetaPeriodo: Record<Periodo, string> = {
  '4semanas': '4 semanas',
  '3meses': '3 meses',
  todo: 'Todo',
}

const tiempo = (iso: string) => parsearFechaUtc(iso).getTime()

// Pesos del periodo elegido, de más antiguo a más reciente.
export function pesosDelPeriodo(
  registros: PesoDto[],
  periodo: Periodo,
  ahora = new Date(),
): PesoDto[] {
  const ordenados = [...registros].sort((a, b) => tiempo(a.fecha) - tiempo(b.fecha))
  if (periodo === 'todo') return ordenados
  // Desde el inicio del día UTC de hace N días: un peso de las 00:00Z de ese día sigue dentro.
  const desde = Math.floor(ahora.getTime() / DIA_MS) * DIA_MS - DIAS_DEL_PERIODO[periodo] * DIA_MS
  return ordenados.filter((r) => tiempo(r.fecha) >= desde)
}

export function puntosReales(registros: PesoDto[]): PuntoGrafica[] {
  return registros.map((r) => ({ t: tiempo(r.fecha), v: r.pesoKg }))
}

// Línea del plan: recta desde el PRIMER peso registrado hasta el objetivo en la fecha en que
// termina el plan, de modo que su pendiente no depende del periodo elegido. `desde` solo la
// recorta por la izquierda (con el mismo valor interpolado) cuando el periodo muestra menos.
// Sin objetivo, sin plan activo o con el plan ya vencido no hay línea.
export function lineaDelPlan(
  reales: PuntoGrafica[],
  objetivoKg: number | null,
  finDelPlan: number | null,
  desde?: number,
): PuntoGrafica[] | undefined {
  if (reales.length === 0 || objetivoKg === null || finDelPlan === null) return undefined
  // Una fecha de plan mal formada llega como NaN: sin línea antes que una escala rota.
  if (!Number.isFinite(finDelPlan)) return undefined
  const inicio = reales[0]
  if (finDelPlan <= inicio.t) return undefined
  const fin = { t: finDelPlan, v: objetivoKg }
  if (desde === undefined || desde <= inicio.t || desde >= finDelPlan) return [inicio, fin]
  const fraccion = (desde - inicio.t) / (finDelPlan - inicio.t)
  return [{ t: desde, v: inicio.v + (objetivoKg - inicio.v) * fraccion }, fin]
}

export interface ResumenPeso {
  inicial: number
  actual: number
  // Negativo si bajó. Redondeado a 1 decimal para no mostrar ruido de la resta.
  cambio: number
  // Días entre el primer y el último registro; las semanas se redondean desde aquí.
  dias: number
  semanas: number
}

export function resumenDePeso(registros: PesoDto[]): ResumenPeso | null {
  if (registros.length === 0) return null
  const primero = registros[0]
  const ultimo = registros[registros.length - 1]
  const dias = Math.max(0, Math.round((tiempo(ultimo.fecha) - tiempo(primero.fecha)) / DIA_MS))
  return {
    inicial: primero.pesoKg,
    actual: ultimo.pesoKg,
    cambio: Math.round((ultimo.pesoKg - primero.pesoKg) * 10) / 10,
    dias,
    semanas: Math.max(0, Math.round(dias / 7)),
  }
}

export interface FilaMedida {
  zona: Zona
  inicio: number
  hoy: number
  // Positivo si subió; redondeado a 1 decimal.
  cambio: number
}

// Primera y última medición de cada zona. Las zonas sin mediciones no aparecen.
export function filasDeMedidas(mediciones: MedicionDto[]): FilaMedida[] {
  const ordenadas = [...mediciones].sort((a, b) => tiempo(a.fecha) - tiempo(b.fecha))
  return ZONAS.flatMap((zona) => {
    const de = ordenadas.filter((m) => m.zona === zona)
    if (de.length === 0) return []
    const inicio = de[0].valor
    const hoy = de[de.length - 1].valor
    return [{ zona, inicio, hoy, cambio: Math.round((hoy - inicio) * 10) / 10 }]
  })
}

// "9 semanas", "1 semana", "5 días": con menos de una semana, redondear a 0 semanas confunde.
export function textoDuracion(dias: number, semanas: number): string {
  if (dias < 7) return `${dias} ${dias === 1 ? 'día' : 'días'}`
  return `${semanas} ${semanas === 1 ? 'semana' : 'semanas'}`
}

// Instante en que termina el plan: inicio del plan más las semanas de la meta.
export function instanteFinDelPlan(
  inicioPlan: string | null | undefined,
  semanas: number | undefined,
): number | null {
  if (!inicioPlan || !semanas) return null
  return tiempo(inicioPlan) + semanas * 7 * DIA_MS
}
