// Documentos legales versionados (docs/mejoras §3, "Páginas legales y de confianza"). El
// consentimiento del registro guarda la versión aceptada de Términos y Privacidad, así que
// cambiar una versión aquí obliga a pedir de nuevo la aceptación. Cuando exista el Manejador
// de documentos, estos textos vendrán del backend (docs/mejoras §6.2).
//
// Texto de demostración: la redacción definitiva es responsabilidad del titular del servicio y
// debe revisarse contra la Ley 172-13 de República Dominicana (docs/mejoras §7.3).
export const DOCUMENTOS_LEGALES = ['terminos', 'privacidad', 'cookies', 'salud'] as const
export type DocumentoLegal = (typeof DOCUMENTOS_LEGALES)[number]

export interface SeccionLegal {
  titulo: string
  parrafos: string[]
}

export interface InfoDocumentoLegal {
  clave: DocumentoLegal
  titulo: string
  version: string
  // Instante UTC (RD-11), mostrado con formatearFecha.
  vigenteDesde: string
  secciones: SeccionLegal[]
}

export const VERSION_TERMINOS = 'v2.1'
export const VERSION_PRIVACIDAD = 'v1.4'

export const INFO_LEGAL: Record<DocumentoLegal, InfoDocumentoLegal> = {
  terminos: {
    clave: 'terminos',
    titulo: 'Términos de uso',
    version: VERSION_TERMINOS,
    vigenteDesde: '2026-08-01T00:00:00Z',
    secciones: [
      {
        titulo: 'Qué ofrece HabitTracker Nexus',
        parrafos: [
          'Una herramienta para convertir una meta física o personal en un plan diario medible. Los planes, cálculos y recomendaciones son orientativos.',
        ],
      },
      {
        titulo: 'Tu cuenta',
        parrafos: [
          'Debes tener al menos 16 años. Eres responsable de mantener tu contraseña en secreto. Puedes desactivar o eliminar tu cuenta en cualquier momento desde el Centro de privacidad.',
        ],
      },
      {
        titulo: 'Uso aceptable',
        parrafos: [
          'No intentes acceder a datos de otras personas ni interferir con el servicio. Podemos suspender cuentas que incumplan estas reglas.',
        ],
      },
    ],
  },
  privacidad: {
    clave: 'privacidad',
    titulo: 'Política de privacidad',
    version: VERSION_PRIVACIDAD,
    vigenteDesde: '2026-08-01T00:00:00Z',
    secciones: [
      {
        titulo: 'Qué datos tratamos',
        parrafos: [
          'Datos de cuenta (nombre y correo), tus hábitos y avances y, si los registras, datos de salud como peso, medidas o sueño. Pedimos solo lo necesario.',
        ],
      },
      {
        titulo: 'Para qué',
        parrafos: [
          'Para generar tu plan y mostrarte tu progreso. Las estadísticas que ve el equipo de administración son agregadas y anónimas: nunca ven tus mediciones individuales.',
        ],
      },
      {
        titulo: 'Tus derechos',
        parrafos: [
          'Puedes descargar tus datos, corregirlos, revocar consentimientos opcionales y eliminar tu cuenta desde el Centro de privacidad.',
        ],
      },
    ],
  },
  cookies: {
    clave: 'cookies',
    titulo: 'Política de cookies',
    version: 'v1.0',
    vigenteDesde: '2026-08-01T00:00:00Z',
    secciones: [
      {
        titulo: 'Qué guardamos en tu navegador',
        parrafos: [
          'Solo lo esencial para que la aplicación funcione: tu sesión y tu preferencia de tema. No usamos cookies de publicidad.',
          'Mientras terminas de crear tu cuenta recordamos en este navegador los consentimientos que aceptaste, la meta elegida y, si usaste la calculadora, sus datos. Se borran al terminar el alta y, como máximo, a los 7 días (los datos de la calculadora, a las 48 horas).',
        ],
      },
    ],
  },
  salud: {
    clave: 'salud',
    titulo: 'Aviso de salud',
    version: 'v1.0',
    vigenteDesde: '2026-08-01T00:00:00Z',
    secciones: [
      {
        titulo: 'No es una indicación médica',
        parrafos: [
          'Los planes, calorías y recomendaciones son estimaciones generales. Consulta a un profesional de la salud antes de empezar si tienes una condición previa, una lesión o estás embarazada.',
          'Detén la actividad si sientes dolor, mareo o falta de aire.',
        ],
      },
    ],
  },
}

export function esDocumentoLegal(valor: unknown): valor is DocumentoLegal {
  return typeof valor === 'string' && (DOCUMENTOS_LEGALES as readonly string[]).includes(valor)
}
