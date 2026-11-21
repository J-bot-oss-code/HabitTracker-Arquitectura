import { parsearFechaUtc } from '@/shared/lib/fechas'

// Como «Mi actividad»: la hora de quien mira (la del dispositivo), en 24 horas. El servidor guarda y envía UTC (RD-11).
const formatoCuando = new Intl.DateTimeFormat('es-DO', {
  dateStyle: 'medium',
  timeStyle: 'short',
  hourCycle: 'h23',
})

export const fechaLocal = (iso: string) => formatoCuando.format(parsearFechaUtc(iso))

// El servidor solo da el identificador de quien escribió (la cuenta vive en Control de Acceso): no hay nombre ni correo
// y aquí no se inventa ninguno. Los primeros 8 caracteres bastan para agrupar los mensajes de una misma persona.
export const idCorto = (usuarioId: string) => usuarioId.slice(0, 8)
