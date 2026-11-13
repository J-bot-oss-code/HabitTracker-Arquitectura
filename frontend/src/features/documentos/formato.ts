// Cómo se ve un archivo en una fila: tipo legible y tamaño en KB o MB.
const TIPOS: Record<string, string> = {
  'application/pdf': 'PDF',
  'image/png': 'Imagen',
  'image/jpeg': 'Imagen',
  'text/plain': 'Texto',
  'application/vnd.openxmlformats-officedocument.wordprocessingml.document': 'Word',
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet': 'Excel',
}

export const etiquetaTipo = (tipo: string) => TIPOS[tipo] ?? 'Archivo'

const numero = new Intl.NumberFormat('es-DO', { maximumFractionDigits: 1 })

export function formatearTamano(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024) return `${numero.format(bytes / 1024)} KB`
  return `${numero.format(bytes / (1024 * 1024))} MB`
}
