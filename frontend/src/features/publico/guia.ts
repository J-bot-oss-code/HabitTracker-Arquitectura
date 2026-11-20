import type { NombreIcono } from '@/shared/ui/Icono'

// Contenido de la guía pública «Cómo funciona» (/como-funciona). Tono: constancia sin culpa.
// Nada de peso, medidas ni premios al cuerpo: la app premia la constancia y el regreso (P4).

export interface Ventaja {
  icono: NombreIcono
  titulo: string
  texto: string
}

export const VENTAJAS: Ventaja[] = [
  {
    icono: 'calendario',
    titulo: 'Planes guiados',
    texto:
      'Un catálogo de planes con pocas tareas concretas, pensados para tu jornada. Te recomendamos los que van con tu meta y te explicamos por qué.',
  },
  {
    icono: 'check',
    titulo: 'Check-in diario con versión mínima',
    texto:
      'Cada día marcas lo que hiciste en segundos. Si el día se complica, haces solo lo mínimo y también cuenta.',
  },
  {
    icono: 'correo',
    titulo: 'Recordatorios por correo',
    texto:
      'Si quieres, te escribimos un recordatorio con el botón «Ya lo hice» para marcarlo sin abrir la app. Los activas tú y te das de baja en un clic.',
  },
  {
    icono: 'trofeo',
    titulo: 'Progreso y logros sin culpa',
    texto:
      'Ves tu constancia semana a semana. Las rachas no castigan: un día sin registro no las corta, y volver también se celebra.',
  },
  {
    icono: 'documentos',
    titulo: 'Mis archivos',
    texto:
      'Guarda los documentos de un plan, como una indicación médica, junto al plan al que pertenecen.',
  },
  {
    icono: 'candado',
    titulo: 'Privacidad',
    texto:
      'Tus datos de salud se guardan cifrados y solo se piden con tu consentimiento. Puedes descargar tus datos o dar de baja tu cuenta cuando quieras.',
  },
]

export interface PasoInicio {
  titulo: string
  texto: string
}

export const PASOS: PasoInicio[] = [
  {
    titulo: 'Crea tu cuenta',
    texto: 'Con tu correo y una contraseña, o con Google. Es gratis y toma un minuto.',
  },
  {
    titulo: 'Actívala con el enlace del correo',
    texto:
      'Abre el mensaje que te enviamos y pulsa el enlace: entras directo, sin volver a escribir nada. Si entraste con Google, te saltas este paso.',
  },
  {
    titulo: 'Cuéntanos tu jornada',
    texto:
      'Son tres toques y no te pedimos datos de salud para empezar. Si un plan los necesita, te lo preguntamos entonces y con tu consentimiento.',
  },
  {
    titulo: 'Elige un plan',
    texto: 'Mira los recomendados para ti o explora todo el catálogo. Tú decides cuándo lo harás.',
  },
  {
    titulo: 'Haz tu primer check-in en «Hoy»',
    texto: 'Ahí aparece lo de hoy. Marca lo que hiciste, o solo lo mínimo si es un día difícil.',
  },
]

export interface Pregunta {
  pregunta: string
  respuesta: string
}

export const PREGUNTAS: Pregunta[] = [
  {
    pregunta: '¿Qué pasa si me salto un día?',
    respuesta:
      'Un día sin registro no corta tu racha, y si pasan más días puedes retomar donde quedaste: el regreso también cuenta. Si necesitas parar un tiempo, puedes pausar el plan o ajustarlo.',
  },
  {
    pregunta: '¿Qué es la versión mínima?',
    respuesta:
      'Es la forma más pequeña de cumplir una tarea, por ejemplo un vaso de agua en lugar de la meta del día. En «Hoy» la eliges con «Hoy solo lo mínimo» y cuenta como un día cumplido.',
  },
  {
    pregunta: '¿Puedo entrar con Google?',
    respuesta:
      'Sí, desde «Crear cuenta» o «Iniciar sesión». Si ya tenías una cuenta con ese mismo correo, te pedimos tu contraseña una sola vez para vincular las dos. Si no la recuerdas, usa «¿Olvidaste tu contraseña?» en el inicio de sesión.',
  },
  {
    pregunta: '¿Quién ve mis datos de salud?',
    respuesta:
      'Solo tú. Se guardan cifrados, se piden únicamente cuando un plan los necesita y con tu consentimiento, que puedes retirar cuando quieras.',
  },
  {
    pregunta: '¿Cómo descargo o borro mis datos?',
    respuesta:
      'Con tu sesión abierta, entra a Configuración › Privacidad y datos. Ahí descargas todo en un archivo o pides dar de baja tu cuenta. Si eliges eliminar, tienes 30 días para cancelarlo.',
  },
]
