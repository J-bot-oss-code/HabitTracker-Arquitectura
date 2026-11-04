// Contrato propuesto: sin endpoint en Host/WebApi. Centro de privacidad (docs/mejoras §7.1 y
// especificación §5.5). Lo simula mocks/handlers/propuestas/privacidad.ts.

export const MODOS_BAJA = ['desactivar', 'eliminar'] as const
export type ModoBaja = (typeof MODOS_BAJA)[number]

export interface SolicitudBajaRequest {
  modo: ModoBaja
  // Confirmación de identidad: se pide la contraseña actual.
  password: string
}

export interface SolicitudBajaResponse {
  modo: ModoBaja
  // Instante UTC del borrado definitivo; null al solo desactivar.
  borradoEn: string | null
}

// Días entre pedir la eliminación y el borrado definitivo. Lo fija el servidor; aquí solo se
// anuncia al usuario antes de confirmar (Límite propuesto para el backend, mejora futura).
export const DIAS_DE_GRACIA = 30
