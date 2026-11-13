// Contrato de Host/WebApi: DocumentsController (/api/documents, Manejador de documentos) y
// AdjuntosPlanController (/api/my-plans/.../attachments, HabitTracker). Las fechas viajan en UTC con "Z".

// DocumentoService: 10 MB y lista blanca de extensiones (el servidor deduce el tipo, nunca lo toma del cliente).
export const TAMANO_MAXIMO_BYTES = 10 * 1024 * 1024
export const EXTENSIONES_PERMITIDAS = [
  '.pdf',
  '.png',
  '.jpg',
  '.jpeg',
  '.txt',
  '.docx',
  '.xlsx',
] as const
export const LONGITUD_MAXIMA_NOMBRE = 255
// AdjuntoPlan.MaximoPorPlan.
export const MAXIMO_ADJUNTOS_POR_PLAN = 10

export interface DocumentoDto {
  id: string
  nombre: string
  tipo: string
  tamanoBytes: number
  fechaSubidaUtc: string
  usuarioId: string
  correoUsuario: string
}

// GET /api/my-plans/attachments: qué archivo está en qué plan y si ese plan sigue en curso (Activo o
// En pausa). Con el plan en curso, eliminar el archivo o quitarlo del plan pide un permiso.
export interface VinculoDocumentoDto {
  documentoId: string
  planId: string
  planNombre: string
  enCurso: boolean
}

// GET /api/my-plans/{id}/attachments.
export interface AdjuntoPlanDto {
  documentoId: string
  nombre: string
  tipo: string
  tamanoBytes: number
  fechaSubidaUtc: string
  adjuntadoEnUtc: string
}

export interface AdjuntarArchivoRequest {
  documentoId: string
}

// El permiso para eliminar un archivo vinculado es uno por documento: PermisosDocumentos.EliminarVinculado
// (HabitTracker.Domain), con el id en 32 dígitos hexadecimales, sin guiones.
export const PREFIJO_ELIMINAR_ARCHIVO = 'Documents.DeleteLinked.'

export function claveEliminarArchivo(documentoId: string): string {
  return PREFIJO_ELIMINAR_ARCHIVO + documentoId.replaceAll('-', '').toLowerCase()
}
