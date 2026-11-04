import { descargarArchivo } from '@/shared/lib/descarga'
import type { EventoAuditoriaDto } from './types'
import { etiquetaAccion } from './types'

// Una celda de CSV. Un texto que empieza por = + - @ (o tabulación/retorno) se interpretaría como
// fórmula al abrir el archivo en una hoja de cálculo (inyección de fórmulas): se antepone una
// comilla simple para que sea texto. Los números no se tocan.
export function celdaCsv(valor: string | number | null): string {
  if (valor === null) return ''
  let texto = String(valor)
  if (typeof valor === 'string' && /^[=+\-@\t\r]/.test(texto)) texto = `'${texto}`
  return /[",\r\n]/.test(texto) ? `"${texto.replace(/"/g, '""')}"` : texto
}

export function auditoriaACsv(eventos: EventoAuditoriaDto[]): string {
  const filas: (string | null)[][] = [
    [
      'Fecha (UTC)',
      'Usuario',
      'Acción',
      'Entidad',
      'Id de la entidad',
      'Valor anterior',
      'Valor nuevo',
      'Origen',
    ],
    ...eventos.map((e) => [
      e.fecha,
      e.actor,
      etiquetaAccion[e.accion],
      e.entidad,
      e.entidadId,
      e.valorAnterior,
      e.valorNuevo,
      e.origen,
    ]),
  ]
  return filas.map((f) => f.map(celdaCsv).join(',')).join('\r\n')
}

// "auditoria-2026-11-03.csv": el día es el día UTC.
export function nombreCsv(prefijo: string, ahora = new Date()): string {
  return `${prefijo}-${ahora.toISOString().slice(0, 10)}.csv`
}

export function descargarCsv(nombre: string, contenido: string) {
  descargarArchivo(nombre, ['\uFEFF', contenido], 'text/csv;charset=utf-8')
}
