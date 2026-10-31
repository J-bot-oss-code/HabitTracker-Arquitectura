// RD-11: todas las fechas viajan y se muestran en UTC, igual que en el backend.

// El backend devuelve algunas fechas leídas de SQL sin "Z" (deuda técnica §6.4).
// Se interpretan como UTC para no desplazarlas según la zona del navegador.
export function parsearFechaUtc(iso: string): Date {
  const tieneZona = /([zZ]|[+-]\d{2}:?\d{2})$/.test(iso)
  return new Date(tieneZona ? iso : `${iso}Z`)
}

const formatoFecha = new Intl.DateTimeFormat('es-DO', { dateStyle: 'medium', timeZone: 'UTC' })
const formatoFechaHora = new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium',
  timeStyle: 'short',
  timeZone: 'UTC',
})

export function formatearFecha(iso: string): string {
  return formatoFecha.format(parsearFechaUtc(iso))
}

export function formatearFechaHora(iso: string): string {
  return `${formatoFechaHora.format(parsearFechaUtc(iso))} UTC`
}

// "2026-10-30" (input type=date) → "2026-10-30T00:00:00.000Z".
// Si la fecha es hoy se envía el instante actual: un avance a las 00:00 quedaría antes del
// inicio de un ciclo que empezó hoy (hábito recién creado o reconfigurado) y no contaría.
export function fechaInputAUtcIso(fecha: string, ahora = new Date()): string {
  if (fecha === hoyUtcInput(ahora)) return ahora.toISOString()
  return new Date(`${fecha}T00:00:00.000Z`).toISOString()
}

export function hoyUtcInput(ahora = new Date()): string {
  return ahora.toISOString().slice(0, 10)
}

const formatoDiaSemana = new Intl.DateTimeFormat('es-DO', { weekday: 'short', timeZone: 'UTC' })

// Día de la semana abreviado ("lun", "mar"…) del día UTC que contiene la fecha.
export function etiquetaDiaSemanaUtc(fecha: Date): string {
  return formatoDiaSemana.format(fecha)
}
