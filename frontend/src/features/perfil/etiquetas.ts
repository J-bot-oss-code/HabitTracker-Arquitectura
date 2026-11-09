import type {
  ActividadPerfil,
  Alergia,
  CondicionSalud,
  Experiencia,
  Lugar,
  NivelPrecaucion,
  Objetivo,
  PerfilLaboral,
  PreguntaSeguridad,
  RestriccionSalud,
  SexoPerfil,
  ZonaCorporal,
} from './types'

// Textos en español de los valores que viajan en PascalCase. Tono neutro y sin culpa (P3, P4).

export const ETIQUETA_PERFIL_LABORAL: Record<PerfilLaboral, { titulo: string; detalle: string }> = {
  OficinaDiurna: { titulo: 'Oficina de día', detalle: 'Mucho tiempo sentado, horario fijo.' },
  TurnoNocturno: { titulo: 'Turno de noche', detalle: 'Trabajas de noche y duermes de día.' },
  TurnosRotativos: { titulo: 'Turnos rotativos', detalle: 'Tu horario cambia cada semana.' },
  Remoto: { titulo: 'Desde casa', detalle: 'Trabajas en remoto.' },
  Estudiante: { titulo: 'Estudio', detalle: 'Clases, tareas y exámenes.' },
  TrabajoFisico: {
    titulo: 'Trabajo físico o de pie',
    detalle: 'Te mueves o cargas en tu jornada.',
  },
  SinEmpleo: { titulo: 'No trabajo ahora', detalle: 'Sin jornada fija o jubilado.' },
}

export const ETIQUETA_SEXO: Record<SexoPerfil, string> = { Mujer: 'Mujer', Hombre: 'Hombre' }

export const ETIQUETA_ACTIVIDAD: Record<ActividadPerfil, string> = {
  Sedentaria: 'Sedentaria (casi sin moverme)',
  Ligera: 'Ligera (1–3 días a la semana)',
  Moderada: 'Moderada (3–5 días)',
  Alta: 'Alta (6–7 días)',
  MuyAlta: 'Muy alta (trabajo físico o doble sesión)',
}

export const ETIQUETA_OBJETIVO: Record<Objetivo, string> = {
  PerderPeso: 'Perder peso',
  GanarMasaMuscular: 'Ganar masa muscular',
  DefinirTonificar: 'Definir o tonificar',
  ReducirEstres: 'Reducir el estrés',
  MejorarConcentracion: 'Mejorar la concentración',
  DormirMejor: 'Dormir mejor',
  Hidratarme: 'Tomar más agua',
  MasEnergia: 'Tener más energía',
  PosturaYMovilidad: 'Postura y movilidad',
}

export const ETIQUETA_ZONA: Record<ZonaCorporal, string> = {
  AbdomenYCore: 'Abdomen y core',
  GluteosYPiernas: 'Glúteos y piernas',
  BrazosYHombros: 'Brazos y hombros',
  Espalda: 'Espalda',
}

export const ETIQUETA_EXPERIENCIA: Record<Experiencia, string> = {
  Ninguna: 'Nunca he entrenado',
  Principiante: 'Principiante (menos de 6 meses)',
  Intermedio: 'Intermedio',
}

export const ETIQUETA_LUGAR: Record<Lugar, string> = {
  CasaSinEquipo: 'En casa, sin equipo',
  CasaConEquipoBasico: 'En casa, con equipo básico',
  Gimnasio: 'En el gimnasio',
}

export const PREGUNTA_SEGURIDAD: Record<PreguntaSeguridad, string> = {
  afeccionCardiacaOPresionAlta:
    '¿Un médico te dijo que tienes una afección del corazón o presión alta?',
  dolorDePecho: '¿Sientes dolor en el pecho en reposo, en tu día a día o al hacer actividad?',
  mareosOPerdidaDeConocimiento:
    '¿Te has mareado hasta perder el equilibrio o perdiste el conocimiento en el último año?',
  otraEnfermedadCronica:
    '¿Te diagnosticaron otra enfermedad crónica (por ejemplo, diabetes o asma)?',
  medicacionCronica: '¿Tomas medicamentos recetados para una enfermedad crónica?',
  problemaOseoOArticular:
    '¿Tienes un problema de huesos, articulaciones o músculos que podría empeorar con la actividad?',
  soloActividadSupervisada:
    '¿Un médico te indicó hacer actividad física solo con supervisión médica?',
}

export const ETIQUETA_CONDICION: Record<CondicionSalud, string> = {
  Ninguna: 'Ninguna',
  Hipertension: 'Presión alta',
  Diabetes: 'Diabetes',
  Asma: 'Asma',
  EnfermedadCardiaca: 'Enfermedad del corazón',
  EnfermedadRenal: 'Enfermedad renal',
  Rodilla: 'Lesión o dolor de rodilla',
  EspaldaBaja: 'Dolor de espalda baja',
  Hombro: 'Lesión o dolor de hombro',
  Osteoporosis: 'Osteoporosis',
  Embarazo: 'Embarazo',
  Posparto: 'Posparto (menos de 6 meses)',
  HistorialTrastornoAlimentario: 'Historial de trastorno de la conducta alimentaria',
}

export const ETIQUETA_ALERGIA: Record<Alergia, string> = {
  Ninguna: 'Ninguna',
  Lactosa: 'Lactosa',
  Gluten: 'Gluten o celiaquía',
  Mani: 'Maní',
  FrutosSecos: 'Frutos secos',
  Mariscos: 'Mariscos',
  Pescado: 'Pescado',
  Huevo: 'Huevo',
  Soya: 'Soya',
}

export const ETIQUETA_PRECAUCION: Record<NivelPrecaucion, string> = {
  Normal: 'Puedes empezar cualquier plan apto para tu edad.',
  Precaucion: 'Adaptaremos algunos planes a lo que nos contaste.',
  ConsultarMedico:
    'Te recomendamos consultar a tu médico antes de empezar. Mientras, te mostraremos planes de intensidad baja.',
}

export const ETIQUETA_RESTRICCION: Record<RestriccionSalud, string> = {
  SoloIntensidadBaja: 'Solo planes de intensidad baja',
  SinIntensidadAlta: 'Sin planes de intensidad alta',
  SinImpacto: 'Sin saltos ni impacto',
  SinRestriccionCalorica: 'Sin planes para bajar de peso ni de calorías',
  EvitarZonaRodilla: 'Cuidamos tus rodillas',
  EvitarZonaEspaldaBaja: 'Cuidamos tu espalda baja',
  EvitarZonaHombro: 'Cuidamos tus hombros',
}

// Zona detectada por el navegador (Intl); si no se puede, la de República Dominicana.
export function zonaDelNavegador(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Santo_Domingo'
  } catch {
    return 'America/Santo_Domingo'
  }
}

// Zonas habituales del público objetivo. La detectada se añade si no está en la lista.
export const ZONAS_HORARIAS_COMUNES = [
  'America/Santo_Domingo',
  'America/New_York',
  'America/Puerto_Rico',
  'America/Bogota',
  'America/Mexico_City',
  'Europe/Madrid',
] as const
