// Contrato real de Host/WebApi: GET /api/audit (AuditController, solo Administrador; 400 si un filtro
// es inválido, 403 al Estándar). Registro inmutable de solo lectura (RF-AUD-06, RF-AUD-07): no hay
// ninguna ruta para modificarlo ni borrarlo. Las métricas (metricas.ts) siguen siendo un contrato propuesto.

// Textos exactos que el backend registra en `accion` (texto libre, hasta 100 caracteres). Sirven para
// el desplegable del filtro; la acción ya es legible y se muestra tal cual.
export const ACCIONES_AUDITORIA = [
  'Cambio de rol',
  'Cambio de estado',
  'Restablecimiento de contraseña',
  'Restablecimiento forzado de contraseña',
  'Reactivación de cuenta',
  'Solicitud de baja de cuenta',
  'Cancelación de baja de cuenta',
  'Descarga de datos personales',
  'Aprobación de solicitud de permiso',
  'Rechazo de solicitud de permiso',
  'Subida de documento',
  'Eliminación de documento',
] as const

// RegistroAuditoriaDto: todos los campos son obligatorios (el servidor nunca manda null).
export interface EventoAuditoriaDto {
  id: string
  // Instante UTC (RD-11).
  fecha: string
  usuarioId: string
  // Correo de quien actuó.
  actor: string
  accion: string
  entidad: string
  entidadId: string
  valorAnterior: string
  valorNuevo: string
}

// PaginaAuditoriaDto: `total` cuenta todo lo que cumple los filtros, no solo esta página.
export interface PaginaAuditoriaDto {
  total: number
  pagina: number
  tamano: number
  registros: EventoAuditoriaDto[]
}

// Todos opcionales y combinables.
export interface FiltroAuditoria {
  // El correo CONTIENE el texto (máx. 254).
  usuario?: string
  // Texto exacto de la acción (máx. 100).
  accion?: string
  // Fechas "AAAA-MM-DD" (día UTC), ambas incluidas; si se dan las dos, máx. 366 días.
  desde?: string
  hasta?: string
  // Desde 1.
  pagina?: number
  // 1 a 200 (50 por defecto).
  tamano?: number
}
