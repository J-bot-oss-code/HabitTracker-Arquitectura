// Contrato de Host/WebApi (MyPlansController y TodayController, HabitTracker v2 Fase 4). Los enums viajan
// como texto en PascalCase y las fechas en UTC con "Z". La identidad sale siempre del token: un plan de
// otra persona responde 404 plan_usuario_no_encontrado, igual que uno que no existe.
import type { EstadoHabito, Periodicidad } from '@/features/habits/types'
import type { Ancla } from '@/features/planes/types'

// Activo ⇄ Pausado; Completado (solo desde Activo, al llegar a fechaFinPrevista) y Cancelado son terminales.
export const ESTADOS_PLAN_USUARIO = ['Activo', 'Pausado', 'Completado', 'Cancelado'] as const
export type EstadoPlanUsuario = (typeof ESTADOS_PLAN_USUARIO)[number]

// Una pausa con motivo no corta la racha y, al reanudar, alarga el plan esos mismos días.
export const MOTIVOS_PAUSA = ['Lesion', 'Viaje', 'Enfermedad', 'Trabajo', 'Otro'] as const
export type MotivoPausa = (typeof MOTIVOS_PAUSA)[number]

// Lo que el servidor puede sugerir. "Pausar" se aplica con una transición (lleva motivo), no con /adjust.
export const TIPOS_AJUSTE = ['ModoSuave', 'Extender', 'Pausar'] as const
export type TipoAjuste = (typeof TIPOS_AJUSTE)[number]

// Lo único que acepta POST /api/my-plans/{id}/adjust.
export const AJUSTES_APLICABLES = ['ModoSuave', 'Extender'] as const
export type AjusteAplicable = (typeof AJUSTES_APLICABLES)[number]

// Personal: un plan armado por la persona (todavía no existe; llegará en una fase posterior).
export const ORIGENES_PLAN = ['Catalogo', 'Personal'] as const
export type OrigenPlan = (typeof ORIGENES_PLAN)[number]

// Minimo: hubo avance solo con la versión mínima y todavía no se llegó a la meta del periodo.
export const ESTADOS_TAREA_HOY = ['Pendiente', 'Hecho', 'Minimo'] as const
export type EstadoTareaHoy = (typeof ESTADOS_TAREA_HOY)[number]

// En qué se cuentan la racha y el cumplimiento: días, o semanas en un plan que solo tiene tareas semanales.
export const UNIDADES_SEGUIMIENTO = ['Dias', 'Semanas'] as const
export type UnidadSeguimiento = (typeof UNIDADES_SEGUIMIENTO)[number]

// Logros por conducta, nunca por el cuerpo. Claves cerradas y estables (ClaveLogro).
export const CLAVES_LOGRO = [
  'PrimerAvance',
  'PrimeraSemana',
  'VeinticincoAvances',
  'Constancia21',
  'Constancia30',
  'MetaCumplida',
  'Regreso',
  'MitadDelPlan',
  'PlanCompletado',
] as const
export type ClaveLogro = (typeof CLAVES_LOGRO)[number]

// Constantes de PlanUsuario y SugeridorAjuste: sirven para orientar a la persona; el servidor decide.
export const MAXIMO_PLANES_EN_CURSO = 3
export const MAXIMO_EXTENSIONES = 2
export const DIAS_MODO_SUAVE = 7
export const DIAS_SIN_REGISTRO_PARA_SUGERIR = 4

// `codigo` del ProblemDetails de estos endpoints. 404: plan_usuario_no_encontrado, plan_no_encontrado y
// perfil_no_encontrado; 409: consentimiento_salud_requerido (solo si el plan pide el cuestionario); el
// resto son 400.
export const CODIGOS_MIS_PLANES = [
  'plan_usuario_no_encontrado',
  'plan_no_encontrado',
  'perfil_no_encontrado',
  'consentimiento_salud_requerido',
  'cuestionario_incompleto',
  'plan_no_apto',
  'plan_ya_activo',
  'limite_planes',
  'transicion_plan_invalida',
  'plan_aun_no_termina',
  'limite_extensiones',
  'ajuste_no_disponible',
] as const
export type CodigoMisPlanes = (typeof CODIGOS_MIS_PLANES)[number]

// --- Peticiones ---

// POST /api/my-plans. `hora` (TimeOnly, "HH:mm:ss" o "HH:mm") manda sobre `ancla`; sin ninguna de las
// dos, el recordatorio toma el ancla de la primera tarea del plan.
export interface InscribirseRequest {
  slug: string
  ancla?: Ancla | null
  hora?: string | null
}

// POST /api/my-plans/{id}/transition. `motivoPausa` es obligatorio al pausar. `conservarHabitos` (falso
// por defecto) solo cuenta al completar o cancelar: verdadero deja los hábitos activos; falso los pausa.
export interface CambiarEstadoPlanRequest {
  nuevoEstado: EstadoPlanUsuario
  motivoPausa?: MotivoPausa | null
  conservarHabitos?: boolean
}

export interface AjustarPlanRequest {
  tipo: AjusteAplicable
}

// PUT /api/my-plans/{id}/reminder. Sin `hora` se conserva la que el plan ya tenía.
export interface RecordatorioPlanRequest {
  hora: string | null
  recordatoriosActivos: boolean
}

// --- Respuestas ---

export interface HabitoDePlanDto {
  habitoId: string
  nombre: string
  // Instrucciones copiadas de la tarea del plan.
  descripcion: string | null
  periodicidad: Periodicidad
  cantidadObjetivo: number
  unidad: string
  estado: EstadoHabito
  versionMinima: string | null
  ancla: Ancla | null
  // Hora local "HH:mm" del ancla según la jornada del perfil; null sin perfil o sin ancla.
  horaSugerida: string | null
}

// Completos y con la versión mínima, por separado, sobre las unidades que cuentan de la ventana.
export interface CumplimientoDto {
  diasCompletos: number
  diasMinimos: number
  diasElegibles: number
  // De 0 a 100, con un decimal.
  porcentajeCompleto: number
  porcentajeConMinimo: number
}

export interface ProgresoPlanDto {
  // Un día (o una semana) sin registro no la corta; dos seguidos sí. Las pausas no cuentan.
  racha: number
  rachaMasLarga: number
  // Últimos 7 días (o la última semana, si la unidad es Semanas).
  siete: CumplimientoDto
  // Últimos 30 días (o las últimas 4 semanas).
  treinta: CumplimientoDto
  semanaActual: number
  semanasTotales: number
  diasRestantes: number
  diasCumplidos: number
  diasSinRegistroSeguidos: number
  cumplidoHoy: boolean
  // Semanas ya cerradas con la meta cumplida (5 de 7 días): desbloquean su tip extra.
  semanasCelebradas: number[]
  unidad: UnidadSeguimiento
}

// Solo propone: aplicar un ajuste es un paso aparte que decide la persona.
export interface SugerenciaAjusteDto {
  opciones: TipoAjuste[]
  // Días sin registro de los últimos 7 (se sugiere con 4 o más).
  diasSinRegistro: number
}

// El tip extra solo viaja en las semanas ya celebradas.
export interface SemanaGuiaPlanDto {
  semana: number
  texto: string
  tipExtra: string | null
}

export interface PlanUsuarioDto {
  id: string
  // Slug del plan del catálogo; null en un plan personal.
  slug: string | null
  nombre: string
  origen: OrigenPlan
  estado: EstadoPlanUsuario
  // Motivo de la pausa vigente; null si el plan no está en pausa.
  motivoPausa: MotivoPausa | null
  // Duración del plan del catálogo; semanasTotales suma las extensiones.
  semanas: number
  semanasTotales: number
  extensiones: number
  fechaInicio: string
  fechaFinPrevista: string
  // Hasta cuándo la versión mínima cuenta como día completo; null si nunca se activó.
  modoSuaveHasta: string | null
  // Hora local "HH:mm" (el servidor ya la da formateada); null si el plan no tiene.
  horaRecordatorio: string | null
  recordatoriosActivos: boolean
  habitos: HabitoDePlanDto[]
  progreso: ProgresoPlanDto
  sugerencia: SugerenciaAjusteDto | null
  // Vacía si el plan no trae guía semanal.
  guia: SemanaGuiaPlanDto[]
}

// --- Hoy (GET /api/today) ---

export interface TareaHoyDto {
  habitoId: string
  planId: string
  planNombre: string
  nombre: string
  instrucciones: string | null
  periodicidad: Periodicidad
  cantidadObjetivo: number
  unidad: string
  ancla: Ancla | null
  // Hora local "HH:mm" del ancla; null sin perfil o sin ancla.
  horaSugerida: string | null
  versionMinima: string | null
  estado: EstadoTareaHoy
  // Lo avanzado en el periodo de la tarea: hoy si es diaria, esta semana (lunes a domingo) si es semanal.
  cantidadPeriodo: number
  restante: number
}

export interface PlanHoyDto {
  planId: string
  nombre: string
  semanaActual: number
  semanasTotales: number
  diasRestantes: number
  racha: number
  sugerencia: SugerenciaAjusteDto | null
}

export interface HoyDto {
  // Día local de la persona (zona horaria del perfil), "yyyy-MM-dd".
  fecha: string
  // Las tareas de los planes activos; un plan en pausa no aporta tareas.
  tareas: TareaHoyDto[]
  planes: PlanHoyDto[]
  // Uno al día, según el momento de la persona. Null si no hay un plan activo.
  mensaje: string | null
  // La semana recién cerrada con 5 de 7 días cumplidos, si hay una para celebrar.
  celebracionSemana: number | null
  tipExtra: string | null
  // Logros ganados que la persona todavía no vio celebrados (POST /api/progress/achievements/seen).
  logrosNuevos: ClaveLogro[]
}

// --- Resumen de constancia (GET /api/progress/summary) ---

export interface LogroDto {
  clave: ClaveLogro
  obtenidoEn: string
  visto: boolean
}

// El mes local en curso, solo con los hábitos de los planes.
export interface MesEnNumerosDto {
  diasCumplidos: number
  // Hábitos activos con avance en 3 días del mes o más.
  habitosMantenidos: number
  regresos: number
}

export interface ResumenProgresoDto {
  // La mejor racha vigente entre los planes en curso.
  racha: number
  rachaMasLarga: number
  logros: LogroDto[]
  mes: MesEnNumerosDto
  mensaje: string | null
  planesEnCurso: number
}
