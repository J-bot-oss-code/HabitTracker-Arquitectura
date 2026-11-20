import type { EventoAuditoriaDto } from './types'

// Qué cambió: "Administrador → Estandar", o "→ desactivar" si solo hay valor nuevo. El servidor nunca manda
// null; "-" significa sin valor (p. ej. la descarga de datos).
const hayValor = (v: string) => v !== '' && v !== '-'

export function cambioDeEvento(e: EventoAuditoriaDto): string | null {
  if (hayValor(e.valorAnterior) && hayValor(e.valorNuevo))
    return `${e.valorAnterior} → ${e.valorNuevo}`
  if (hayValor(e.valorNuevo)) return `→ ${e.valorNuevo}`
  return null
}

// --- «Mi actividad»: lo que se guarda en los archivos y lo que se lee en pantalla ---

// Lo que lleva cada registro de los archivos (CSV y JSON): los mismos cuatro campos. "-" (sin valor) sale vacío.
export interface RegistroExportado {
  fecha: string
  accion: string
  antes: string
  despues: string
}

const sinMarca = (v: string) => (hayValor(v) ? v : '')

export function aRegistroExportado(e: EventoAuditoriaDto): RegistroExportado {
  return {
    fecha: e.fecha,
    accion: e.accion,
    antes: sinMarca(e.valorAnterior),
    despues: sinMarca(e.valorNuevo),
  }
}

// "(20480 bytes)" → "(20 KB)" o "(1,5 MB)"; menos de 1 KB se deja como "(menos de 1 KB)".
function tamanoLegible(bytes: number): string {
  if (bytes < 1024) return 'menos de 1 KB'
  if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`
  return `${(bytes / (1024 * 1024)).toLocaleString('es-DO', { maximumFractionDigits: 1 })} MB`
}

function valorParaPersona(v: string): string {
  return v
    .replace(/\((\d+) bytes\)/g, (_, n: string) => `(${tamanoLegible(Number(n))})`)
    .replace(/\bEstandar\b/g, 'Estándar')
}

// Detalle de una acción para leerlo, sin flechas: «Antes: X. Ahora: Y» o «Ahora: Y». Sin detalle si no hay
// valor nuevo o si el servidor avisa que algo «no se guarda» (p. ej. una contraseña). «No existía» no es un
// valor anterior: es la ausencia de uno.
export function detalleParaPersona(e: EventoAuditoriaDto): string | null {
  if (/no se guarda/i.test(`${e.valorAnterior} ${e.valorNuevo}`)) return null
  if (!hayValor(e.valorNuevo)) return null
  const ahora = valorParaPersona(e.valorNuevo)
  const antes = e.valorAnterior.trim().toLowerCase() === 'no existía' ? '' : e.valorAnterior
  return hayValor(antes) ? `Antes: ${valorParaPersona(antes)}. Ahora: ${ahora}` : `Ahora: ${ahora}`
}
