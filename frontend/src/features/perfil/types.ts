// Contrato de Host/WebApi (ProfileController, HabitTracker v2 Fase 2). Los enums viajan como texto en
// PascalCase. El perfil tiene dos niveles: el 1 (jornada, zona horaria y aviso de salud) es obligatorio
// y NO lleva datos de salud; el resto es opcional y se pide cuando un plan lo necesita (P1).

export const PERFILES_LABORALES = [
  'OficinaDiurna',
  'TurnoNocturno',
  'TurnosRotativos',
  'Remoto',
  'Estudiante',
  'TrabajoFisico',
  'SinEmpleo',
] as const
export type PerfilLaboral = (typeof PERFILES_LABORALES)[number]

export const SEXOS_PERFIL = ['Mujer', 'Hombre'] as const
export type SexoPerfil = (typeof SEXOS_PERFIL)[number]

export const ACTIVIDADES_PERFIL = ['Sedentaria', 'Ligera', 'Moderada', 'Alta', 'MuyAlta'] as const
export type ActividadPerfil = (typeof ACTIVIDADES_PERFIL)[number]

export interface PerfilDto {
  // Nombre de la cuenta (solo lectura): sale del token.
  nombre: string
  perfilLaboral: PerfilLaboral
  // Zona IANA, p. ej. "America/Santo_Domingo".
  zonaHoraria: string
  avisoSaludAceptado: boolean
  // Horas locales "HH:mm" o "HH:mm:ss" (TimeOnly del backend).
  horaEntradaTrabajo: string | null
  horaSalidaTrabajo: string | null
  horaDespertar: string | null
  horaDormir: string | null
  edad: number | null
  // Opcional por minimización de datos (docs/mejoras §7.2).
  sexo: SexoPerfil | null
  // Peso y altura son datos de salud: guardarlos exige el consentimiento datosSalud (409).
  alturaCm: number | null
  pesoKg: number | null
  actividad: ActividadPerfil | null
  diasPorSemana: number | null
  minutosPorSesion: number | null
  actualizadoEn: string
}

export type GuardarPerfilRequest = Omit<PerfilDto, 'nombre' | 'actualizadoEn'>

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

// --- Cuestionario de salud por bloques (GET/PUT /api/profile/health). Listas cerradas, sin texto libre. ---

export const OBJETIVOS = [
  'PerderPeso',
  'GanarMasaMuscular',
  'DefinirTonificar',
  'ReducirEstres',
  'MejorarConcentracion',
  'DormirMejor',
  'Hidratarme',
  'MasEnergia',
  'PosturaYMovilidad',
] as const
export type Objetivo = (typeof OBJETIVOS)[number]
export const MAXIMO_OBJETIVOS = 3

export const ZONAS_CORPORALES = [
  'AbdomenYCore',
  'GluteosYPiernas',
  'BrazosYHombros',
  'Espalda',
] as const
export type ZonaCorporal = (typeof ZONAS_CORPORALES)[number]

export const EXPERIENCIAS = ['Ninguna', 'Principiante', 'Intermedio'] as const
export type Experiencia = (typeof EXPERIENCIAS)[number]

export const LUGARES = ['CasaSinEquipo', 'CasaConEquipoBasico', 'Gimnasio'] as const
export type Lugar = (typeof LUGARES)[number]

export const CONDICIONES = [
  'Ninguna',
  'Hipertension',
  'Diabetes',
  'Asma',
  'EnfermedadCardiaca',
  'EnfermedadRenal',
  'Rodilla',
  'EspaldaBaja',
  'Hombro',
  'Osteoporosis',
  'Embarazo',
  'Posparto',
  'HistorialTrastornoAlimentario',
] as const
export type CondicionSalud = (typeof CONDICIONES)[number]

export const ALERGIAS = [
  'Ninguna',
  'Lactosa',
  'Gluten',
  'Mani',
  'FrutosSecos',
  'Mariscos',
  'Pescado',
  'Huevo',
  'Soya',
] as const
export type Alergia = (typeof ALERGIAS)[number]

export interface BloqueObjetivos {
  objetivos: Objetivo[]
  zonas: ZonaCorporal[]
  experiencia: Experiencia
  lugar: Lugar
}

// Revisión de seguridad: 7 preguntas sí/no inspiradas en el PAR-Q+ (redacción propia).
export const PREGUNTAS_SEGURIDAD = [
  'afeccionCardiacaOPresionAlta',
  'dolorDePecho',
  'mareosOPerdidaDeConocimiento',
  'otraEnfermedadCronica',
  'medicacionCronica',
  'problemaOseoOArticular',
  'soloActividadSupervisada',
] as const
export type PreguntaSeguridad = (typeof PREGUNTAS_SEGURIDAD)[number]
export type BloqueSeguridad = Record<PreguntaSeguridad, boolean>

export interface BloqueCondiciones {
  condiciones: CondicionSalud[]
  alergias: Alergia[]
}

export type NivelPrecaucion = 'Normal' | 'Precaucion' | 'ConsultarMedico'
export type RestriccionSalud =
  | 'SoloIntensidadBaja'
  | 'SinIntensidadAlta'
  | 'SinImpacto'
  | 'SinRestriccionCalorica'
  | 'EvitarZonaRodilla'
  | 'EvitarZonaEspaldaBaja'
  | 'EvitarZonaHombro'

export interface EvaluacionSalud {
  nivel: NivelPrecaucion
  restricciones: RestriccionSalud[]
  alergenosAEvitar: Alergia[]
  completitud: { objetivos: boolean; seguridad: boolean; condiciones: boolean }
}

export interface CuestionarioSaludDto {
  version: number
  objetivos: BloqueObjetivos | null
  objetivosRespondidoEn: string | null
  seguridad: BloqueSeguridad | null
  seguridadRespondidoEn: string | null
  condiciones: BloqueCondiciones | null
  condicionesRespondidoEn: string | null
  evaluacion: EvaluacionSalud
  // Falso si la persona retiró el consentimiento datosSalud: sus respuestas se ven pero no se usan.
  usaDatosDeSalud: boolean
}
