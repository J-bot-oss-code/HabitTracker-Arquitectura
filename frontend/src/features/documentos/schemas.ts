import { z } from 'zod'
import { EXTENSIONES_PERMITIDAS, LONGITUD_MAXIMA_NOMBRE, TAMANO_MAXIMO_BYTES } from './types'

// Mismas reglas que DocumentoService (RD-07: el servidor valida de nuevo, y es quien mide los bytes de verdad).
const extensionDe = (nombre: string) => {
  const punto = nombre.lastIndexOf('.')
  return punto < 0 ? '' : nombre.slice(punto).toLowerCase()
}

export const archivoSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'El archivo necesita un nombre.')
    .max(LONGITUD_MAXIMA_NOMBRE, 'El nombre del archivo es demasiado largo.')
    .refine(
      (nombre) => (EXTENSIONES_PERMITIDAS as readonly string[]).includes(extensionDe(nombre)),
      'Este tipo de archivo no se puede guardar. Sirven PDF, PNG, JPG, TXT, DOCX y XLSX.',
    ),
  size: z
    .number()
    .min(1, 'El archivo está vacío. Elige otro.')
    .max(TAMANO_MAXIMO_BYTES, 'El archivo pesa más de 10 MB. Prueba con uno más ligero.'),
})

// Texto para mostrar antes de subir, o null si el archivo sirve. Acepta un File o cualquier cosa con nombre y tamaño.
export function errorDeArchivo(
  archivo: Pick<File, 'name' | 'size'> | null | undefined,
): string | null {
  if (!archivo) return 'Elige un archivo para subir.'
  const resultado = archivoSchema.safeParse({ name: archivo.name, size: archivo.size })
  return resultado.success ? null : resultado.error.issues[0].message
}
