// Contrato propuesto: sin endpoint en Host/WebApi. Perfil físico (onboarding, docs/mejoras §3)
// y consentimientos versionados (`ConsentRecord`, docs/mejoras §3 y §7). Los simula
// mocks/handlers/propuestas/perfil.ts y están registrados en CONTRATOS_PROPUESTOS.
import type { NivelActividad, Sexo } from '@/features/publico/types'

export interface PerfilDto {
  // Nombre de la cuenta (solo lectura): /api/auth/me no lo devuelve.
  nombre: string
  edad: number
  // Opcional por minimización de datos (docs/mejoras §7.2).
  sexo: Sexo | null
  alturaCm: number
  pesoKg: number
  actividad: NivelActividad
  diasPorSemana: number
  minutosPorSesion: number
  // Meta del catálogo público; null si el usuario decide más tarde.
  metaSlug: string | null
  // Instante UTC en que empezó el plan de la meta actual; lo fija el servidor.
  inicioPlan: string | null
  avisoSaludAceptado: boolean
}

export type GuardarPerfilRequest = Omit<PerfilDto, 'nombre' | 'inicioPlan'>

export interface ConsentimientosDto {
  terminosVersion: string | null
  privacidadVersion: string | null
  // Instante UTC de la última aceptación de versiones; lo fija el servidor.
  aceptadoEn: string | null
  datosSalud: boolean
  marketing: boolean
  compartirCumplimiento: boolean
  estadisticasAnonimas: boolean
}

export type GuardarConsentimientosRequest = Omit<ConsentimientosDto, 'aceptadoEn'>
