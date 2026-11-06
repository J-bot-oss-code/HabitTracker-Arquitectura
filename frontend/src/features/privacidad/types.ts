// Contrato de PrivacidadController (/api/privacy). Ver Host/WebApi/Controllers/PrivacidadController.cs.

export const MODOS_BAJA = ['desactivar', 'eliminar'] as const
export type ModoBaja = (typeof MODOS_BAJA)[number]

// Confirmación de identidad: la contraseña o, si la cuenta no tiene (solo entra con Google), el ID token de la
// cuenta de Google vinculada. Una confirmación incorrecta es un 400 que cuenta para el bloqueo, nunca un 401.
export interface SolicitudBajaRequest {
  modo: ModoBaja
  password?: string
  idTokenGoogle?: string
}

export interface SolicitudBajaResponse {
  modo: ModoBaja
  // Instante UTC del borrado definitivo; null al solo desactivar.
  borradoEn: string | null
}

// GET /api/privacy/deletion-request (404 si no hay una eliminación programada).
export interface BajaProgramadaResponse {
  borradoEn: string
}

// Días entre pedir la eliminación y el borrado definitivo. Lo fija el servidor (Privacidad:DiasGraciaBaja, 30 por
// defecto): aquí solo se anuncia antes de confirmar; la fecha real llega en la respuesta.
export const DIAS_DE_GRACIA = 30

// GET /api/privacy/export: todo lo que el sistema guarda de la persona. Nunca trae la contraseña, su hash ni los tokens.
export interface ExportacionServidor {
  generadoEn: string
  cuenta: {
    id: string
    nombre: string
    correo: string
    rol: string
    activo: boolean
    fechaCreacion: string
    tienePassword: boolean
    desactivadaPorUsuarioEn: string | null
    bajaProgramadaPara: string | null
  }
  consentimientos: unknown[]
  vinculos: unknown[]
  sesiones: unknown[]
  // Una sección por módulo que guarda datos de la persona (hábitos, permisos, documentos).
  modulos: Record<string, unknown>
}
