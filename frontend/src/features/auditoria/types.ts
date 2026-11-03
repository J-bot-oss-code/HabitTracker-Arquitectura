// Contrato propuesto: sin endpoint en Host/WebApi. Registro de auditoría (RF-AUD-01, RF-AUD-07 y
// docs/mejoras §6.1). Lo simula mocks/handlers/propuestas/admin.ts y está registrado en
// CONTRATOS_PROPUESTOS. Solo lectura y solo para el Administrador (RF-AUD-06: el registro no
// se modifica ni se elimina desde la aplicación).

export const ACCIONES_AUDITORIA = [
  'Registro',
  'InicioSesion',
  'CambioRol',
  'CambioEstado',
  'ResolucionPermiso',
  'DescargaDatos',
  'SolicitudBaja',
] as const
export type AccionAuditoria = (typeof ACCIONES_AUDITORIA)[number]

export const etiquetaAccion: Record<AccionAuditoria, string> = {
  Registro: 'Registro de cuenta',
  InicioSesion: 'Inicio de sesión',
  CambioRol: 'Cambio de rol',
  CambioEstado: 'Cambio de estado de cuenta',
  ResolucionPermiso: 'Resolución de permiso',
  DescargaDatos: 'Descarga de datos',
  SolicitudBaja: 'Solicitud de baja',
}

export interface EventoAuditoriaDto {
  id: string
  // Instante UTC (RD-11).
  fecha: string
  // Correo de quien actuó.
  actor: string
  accion: AccionAuditoria
  entidad: string
  entidadId: string | null
  valorAnterior: string | null
  valorNuevo: string | null
  origen: string
}

export interface FiltroAuditoria {
  actor?: string
  accion?: AccionAuditoria
  // Fechas "AAAA-MM-DD" (día UTC), ambas incluidas.
  desde?: string
  hasta?: string
  limite?: number
}
