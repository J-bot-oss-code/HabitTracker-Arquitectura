import { esSlugPlan } from '@/features/planes/api'
import {
  validarDatosCorporales,
  VERSION_PRIVACIDAD,
  VERSION_TERMINOS,
} from '@/features/publico/api'
import type { DatosCorporales } from '@/features/publico/types'

// Entre el registro y el primer inicio de sesión hay una activación por correo que suele
// abrirse en otra pestaña. Se recuerdan en este dispositivo hasta completar el onboarding: versiones
// aceptadas, el consentimiento opcional de correos, la meta elegida y, si vino de la
// calculadora, sus datos para proponerlos. Si falta (otro dispositivo), el onboarding vuelve a
// pedir la aceptación.
//
// Minimización: el registro caduca a los 7 días y los datos corporales a las 48 horas. Lo
// caducado o mal formado se borra al leerlo y también al arrancar la aplicación
// (purgarRegistroPendienteCaducado), aunque nadie vuelva a entrar en este equipo.
// El correo NO se guarda en claro (SEG-17): solo una huella para comprobar que es de quien entra.
const CLAVE = 'habittracker.registro-pendiente'
const HORA_MS = 60 * 60 * 1000
export const CADUCIDAD_REGISTRO_MS = 7 * 24 * HORA_MS
export const CADUCIDAD_DATOS_CORPORALES_MS = 48 * HORA_MS

export interface RegistroPendiente {
  correo: string
  terminosVersion: string
  privacidadVersion: string
  marketing: boolean
  metaSlug: string | null
  datosCorporales: DatosCorporales | null
  guardadoEn: number
}

export function guardarRegistroPendiente(datos: Omit<RegistroPendiente, 'guardadoEn'>) {
  try {
    const registro: RegistroPendiente = {
      ...datos,
      correo: huella(datos.correo),
      guardadoEn: Date.now(),
    }
    localStorage.setItem(CLAVE, JSON.stringify(registro))
  } catch {
    // Almacenamiento bloqueado: el onboarding pedirá de nuevo la aceptación.
  }
}

// FNV-1a de 32 bits del correo normalizado: basta para reconocer a la misma cuenta en este equipo sin dejar el correo legible.
function huella(correo: string): string {
  let h = 0x811c9dc5
  for (const c of correo.trim().toLowerCase()) {
    h ^= c.codePointAt(0)!
    h = Math.imul(h, 0x01000193) >>> 0
  }
  return 'h:' + h.toString(16)
}

export function borrarRegistroPendiente() {
  try {
    localStorage.removeItem(CLAVE)
  } catch {
    // Nada que borrar.
  }
}

function esRegistroPendiente(valor: unknown): valor is RegistroPendiente {
  if (!valor || typeof valor !== 'object') return false
  const r = valor as Record<string, unknown>
  return (
    typeof r.correo === 'string' &&
    typeof r.terminosVersion === 'string' &&
    typeof r.privacidadVersion === 'string' &&
    typeof r.marketing === 'boolean' &&
    (r.metaSlug === null || typeof r.metaSlug === 'string') &&
    typeof r.guardadoEn === 'number'
  )
}

// Lee el registro guardado; si está mal formado, caducado o fechado en el futuro, lo borra.
function leerValido(ahora: number): RegistroPendiente | null {
  let crudo: unknown
  try {
    const texto = localStorage.getItem(CLAVE)
    if (texto === null) return null
    crudo = JSON.parse(texto)
  } catch {
    borrarRegistroPendiente()
    return null
  }
  const edad = esRegistroPendiente(crudo) ? ahora - crudo.guardadoEn : Number.NaN
  if (!esRegistroPendiente(crudo) || !(edad >= 0 && edad <= CADUCIDAD_REGISTRO_MS)) {
    borrarRegistroPendiente()
    return null
  }
  return crudo
}

export function purgarRegistroPendienteCaducado(ahora = Date.now()) {
  leerValido(ahora)
}

// Solo vale para la cuenta que lo creó y antes de caducar. El plan (por su formato) y los datos
// corporales (por sus límites) se revalidan, porque localStorage es editable. Que el plan exista en el
// catálogo lo comprueba quien lo usa (AuthLayout y el onboarding), que tiene la lista.
export function leerRegistroPendiente(
  correo: string,
  ahora = Date.now(),
): RegistroPendiente | null {
  const registro = leerValido(ahora)
  if (!registro || registro.correo !== huella(correo)) return null
  const datosVigentes = ahora - registro.guardadoEn <= CADUCIDAD_DATOS_CORPORALES_MS
  return {
    ...registro,
    metaSlug: esSlugPlan(registro.metaSlug) ? registro.metaSlug : null,
    datosCorporales: datosVigentes ? validarDatosCorporales(registro.datosCorporales) : null,
  }
}

// Las versiones recordadas solo valen si siguen siendo las vigentes: no se registra la
// aceptación de un documento que el usuario no vio.
export function aceptoVersionesVigentes(registro: RegistroPendiente): boolean {
  return (
    registro.terminosVersion === VERSION_TERMINOS &&
    registro.privacidadVersion === VERSION_PRIVACIDAD
  )
}
