// Base de datos en memoria de la API simulada. Solo existe en el navegador (modo demo)
// o en las pruebas; nunca sustituye la persistencia real (RD-09 lo cumple el backend).
import type { Periodicidad, EstadoHabito } from '@/features/habits/types'
import type { EventoAuditoriaDto } from '@/features/auditoria/types'
import type {
  ClaveLogro,
  EstadoPlanUsuario,
  MotivoPausa,
  OrigenPlan,
} from '@/features/mis-planes/types'
import type { ConsentimientosDto, CuestionarioSaludDto, PerfilDto } from '@/features/perfil/types'
import type { Ancla } from '@/features/planes/types'
import type { MedicionDto, PesosDto } from '@/features/progreso/types'
import type { SolicitudPermisoDto } from '@/features/permissions/types'
import type { Rol } from '@/shared/auth/roles'
import { PLANES_MOCK } from './handlers/planesData'

export const PASSWORD_DEMO = 'Demo1234'
export const META_DEMO_ID = '3f2b8c1e-6a4d-4e9b-9c7a-1d2e3f4a5b6c'
// Códigos fijos para poder probar los flujos de correo sin servidor SMTP.
// El código de activación real son 6 dígitos (ActivateRequest.Codigo).
export const TOKEN_ACTIVACION_DEMO = '123456'
export const CODIGO_RECUPERACION_DEMO = 'DEMO-123456'
// Plan que Ana ya sigue en los datos de ejemplo ("Mi día de oficina saludable", en su semana 2 de 4).
export const PLAN_DEMO_ID = '6d1a2b3c-4d5e-4f60-8a7b-9c0d1e2f3a01'
export const PLAN_DEMO_SLUG = 'mi-dia-oficina'

export interface UsuarioMock {
  id: string
  nombre: string
  correo: string
  password: string | null
  rol: Rol
  activo: boolean
  intentosFallidos: number
  bloqueadoHasta: number | null
  tokenActivacion: string | null
  codigoRecuperacion: string | null
  // Tokens emitidos antes de este instante (ms) quedan invalidados (RF-CA-12, RF-CA-21).
  sesionesValidasDesde: number
  // La propia persona desactivó su cuenta (no un administrador): un inicio de sesión correcto la reactiva.
  desactivadaPorUsuario?: boolean
}

export interface RegistroMock {
  id: string
  cantidad: number
  fecha: string
  unidad: string
  // "Hoy solo lo mínimo": solo en un hábito de un plan.
  esVersionMinima?: boolean
}

export interface HabitoMock {
  id: string
  usuarioId: string
  nombre: string
  periodicidad: Periodicidad
  cantidadObjetivo: number
  unidad: string
  estado: EstadoHabito
  metaId?: string
  fechaInicioCiclo: string
  registros: RegistroMock[]
  // Hábito nacido de una tarea de un plan (Habito.DesdeTareaDePlan). Sin ellos es un hábito suelto.
  planUsuarioId?: string
  descripcion?: string
  versionMinima?: string
  ancla?: Ancla
  // Lo pausó el plan (al pausarse o al terminar), no la persona: solo esos vuelven al reanudar el plan.
  pausadoPorPlan?: boolean
}

// Un tramo en que el plan estuvo en pausa (PeriodoPausa): esos días no cuentan para la racha.
export interface PausaPlanMock {
  inicioUtc: string
  // Null mientras la pausa sigue abierta.
  finUtc: string | null
  motivo: MotivoPausa
}

// El plan que una persona sigue (PlanUsuario). Sus hábitos están en db.habitos con planUsuarioId.
export interface PlanUsuarioMock {
  id: string
  usuarioId: string
  slug: string | null
  nombre: string
  origen: OrigenPlan
  estado: EstadoPlanUsuario
  motivoPausa: MotivoPausa | null
  semanas: number
  extensiones: number
  fechaInicio: string
  fechaFinPrevista: string
  modoSuaveHasta: string | null
  // Hora local "HH:mm".
  horaRecordatorio: string | null
  recordatoriosActivos: boolean
  pausas: PausaPlanMock[]
}

// Un logro de una persona (LogroUsuario): único por persona y clave, y se felicita una sola vez.
export interface LogroMock {
  usuarioId: string
  clave: ClaveLogro
  obtenidoEn: string
  vistoEn: string | null
}

// --- Contratos propuestos (sin backend todavía; ver handlers/propuestas) ---
export type PerfilMock = Omit<PerfilDto, 'nombre'>
// El cuestionario se guarda por bloques; la evaluación se calcula al leerlo (como EvaluadorSalud).
export type CuestionarioMock = Omit<
  CuestionarioSaludDto,
  'version' | 'evaluacion' | 'usaDatosDeSalud'
>

// Baja de cuenta pedida (modo "eliminar"): mientras dura el periodo de gracia la cuenta sigue activa y se puede
// cancelar; al terminar, el proceso del backend borra todo. Desactivar no la programa: queda inactiva.
export interface BajaProgramadaMock {
  solicitadaEn: string
  // Fin del periodo de gracia (UTC).
  borradoEn: string
}

// Una sesión abierta en un dispositivo (SesionUsuario): el refresh token rotativo la sostiene.
export interface SesionMock {
  id: string
  usuarioId: string
  dispositivo: string
  creadaEn: string
  ultimoUsoEn: string
}

interface Db {
  usuarios: UsuarioMock[]
  habitos: HabitoMock[]
  // Mis planes (HabitTracker v2, Fase 4).
  planesUsuario: PlanUsuarioMock[]
  logros: LogroMock[]
  solicitudes: SolicitudPermisoDto[]
  metas: Set<string>
  tokensRevocados: Set<string>
  // Cookie HttpOnly del refresh token (una sola: el modo demo tiene un único navegador).
  refresh: { usuarioId: string; valor: string; sesionId: string } | null
  sesiones: SesionMock[]
  // Vínculo con Google por id de usuario (LoginsExternos: una identidad por cuenta y una cuenta por identidad).
  vinculosGoogle: Map<string, { subject: string; vinculadoEn: string }>
  // Por id de usuario.
  perfiles: Map<string, PerfilMock>
  cuestionarios: Map<string, CuestionarioMock>
  consentimientos: Map<string, ConsentimientosDto>
  pesos: Map<string, PesosDto>
  mediciones: Map<string, MedicionDto[]>
  auditoria: EventoAuditoriaDto[]
  bajas: Map<string, BajaProgramadaMock>
}

export const db: Db = {
  usuarios: [],
  habitos: [],
  planesUsuario: [],
  logros: [],
  solicitudes: [],
  metas: new Set(),
  tokensRevocados: new Set(),
  refresh: null,
  sesiones: [],
  vinculosGoogle: new Map(),
  perfiles: new Map(),
  cuestionarios: new Map(),
  consentimientos: new Map(),
  pesos: new Map(),
  mediciones: new Map(),
  auditoria: [],
  bajas: new Map(),
}

const DIA = 24 * 60 * 60 * 1000

function usuario(
  datos: Partial<UsuarioMock> & Pick<UsuarioMock, 'id' | 'nombre' | 'correo' | 'rol'>,
): UsuarioMock {
  return {
    password: PASSWORD_DEMO,
    activo: true,
    intentosFallidos: 0,
    bloqueadoHasta: null,
    tokenActivacion: null,
    codigoRecuperacion: null,
    sesionesValidasDesde: 0,
    ...datos,
  }
}

function inicioDelDiaUtc(desplazamientoDias: number, ahora = Date.now()): string {
  const fecha = new Date(ahora + desplazamientoDias * DIA)
  return new Date(
    Date.UTC(fecha.getUTCFullYear(), fecha.getUTCMonth(), fecha.getUTCDate()),
  ).toISOString()
}

const logroVisto = (
  usuarioId: string,
  clave: ClaveLogro,
  dias: number,
  ahora: number,
): LogroMock => ({
  usuarioId,
  clave,
  obtenidoEn: new Date(ahora - dias * DIA).toISOString(),
  vistoEn: new Date(ahora - dias * DIA).toISOString(),
})

// Plan de ejemplo de Ana: sigue "Mi día de oficina saludable" desde hace 9 días (semana 2 de 4, racha
// de 9), con un día sin registro (no corta la racha), uno con la versión mínima, la semana 1 celebrada y
// hoy a medias (una tarea hecha, una con la mínima y otra pendiente). Así Hoy, Mis planes y Progreso se
// ven con contenido.
//
// La demo del navegador lo siembra siempre. En las pruebas es opcional: agrega 3 hábitos activos a Ana y
// las pruebas anteriores a la Fase 4 cuentan con que solo tenga los suyos; las que necesitan el plan
// llaman a sembrarPlanDemo() al empezar (reiniciarDb() lo quita después de cada prueba).
export function sembrarPlanDemo(ahora = Date.now()) {
  if (db.planesUsuario.some((p) => p.id === PLAN_DEMO_ID)) return
  const estandar = db.usuarios[0].id
  const plantilla = PLANES_MOCK.find((p) => p.slug === PLAN_DEMO_SLUG)!
  // Por día (0 = el día en que empezó, 9 = hoy) y por tarea del plan: la cantidad avanzada, 'min' (un
  // avance con la versión mínima) o null (sin avance).
  const avances: (number | 'min' | null)[][] = [
    [8, 3, 3],
    [8, 3, 3],
    [6, 2, null],
    [null, null, null],
    [8, 3, 3],
    [8, 'min', 3],
    [8, 3, 3],
    [8, 3, 3],
    [5, null, 3],
    [8, 'min', null],
  ]
  const inicio = ahora - (avances.length - 1) * DIA
  db.planesUsuario.push({
    id: PLAN_DEMO_ID,
    usuarioId: estandar,
    slug: plantilla.slug,
    nombre: plantilla.nombre,
    origen: 'Catalogo',
    estado: 'Activo',
    motivoPausa: null,
    semanas: plantilla.semanas,
    extensiones: 0,
    fechaInicio: new Date(inicio).toISOString(),
    fechaFinPrevista: new Date(inicio + plantilla.semanas * 7 * DIA).toISOString(),
    modoSuaveHasta: null,
    // "Al llegar al trabajo" con la entrada de Ana a las 08:00.
    horaRecordatorio: '08:00',
    recordatoriosActivos: false,
    pausas: [],
  })
  db.habitos.push(
    ...plantilla.tareas.map((tarea, t): HabitoMock => ({
      id: `7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e1${t + 1}`,
      usuarioId: estandar,
      nombre: tarea.nombre,
      periodicidad: tarea.periodicidad,
      cantidadObjetivo: tarea.cantidad,
      unidad: tarea.unidad,
      estado: 'Activo',
      fechaInicioCiclo: new Date(inicio).toISOString(),
      planUsuarioId: PLAN_DEMO_ID,
      descripcion: tarea.instrucciones,
      versionMinima: tarea.versionMinima,
      ancla: tarea.anclaSugerida,
      registros: avances.flatMap((delDia, d) => {
        const avance = delDia[t]
        if (avance === null) return []
        return [
          {
            id: `ee000000-0000-4000-8000-${String(d * 10 + t).padStart(12, '0')}`,
            cantidad: avance === 'min' ? 1 : avance,
            // A la hora de ahora, N días atrás: cae en ese día local sea cual sea la zona del perfil.
            fecha: new Date(inicio + d * DIA).toISOString(),
            unidad: tarea.unidad,
            ...(avance === 'min' ? { esVersionMinima: true } : {}),
          },
        ]
      }),
    })),
  )
  // Los logros que esa historia ya ganó, celebrados: Hoy no abre con una felicitación pendiente.
  db.logros.push(
    logroVisto(estandar, 'PrimeraSemana', 2, ahora),
    logroVisto(estandar, 'VeinticincoAvances', 1, ahora),
  )
}

export function reiniciarDb() {
  const ahora = Date.now()
  db.usuarios = [
    usuario({
      id: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a01',
      nombre: 'Ana Estándar',
      correo: 'estandar@demo.com',
      rol: 'Estandar',
    }),
    usuario({
      id: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a02',
      nombre: 'Admin Demo',
      correo: 'admin@demo.com',
      rol: 'Administrador',
    }),
    usuario({
      id: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a03',
      nombre: 'Luis Otro',
      correo: 'otro@demo.com',
      rol: 'Estandar',
    }),
    usuario({
      id: '0b8e7a52-1c3d-4f6e-8a9b-1c2d3e4f5a04',
      nombre: 'Cuenta Sin Activar',
      correo: 'inactiva@demo.com',
      rol: 'Estandar',
      activo: false,
      tokenActivacion: TOKEN_ACTIVACION_DEMO,
    }),
  ]

  const estandar = db.usuarios[0].id
  const inicioCiclo = new Date(ahora - 10 * DIA).toISOString()
  db.habitos = [
    {
      id: '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e01',
      usuarioId: estandar,
      nombre: 'Leer',
      periodicidad: 'Diaria',
      cantidadObjetivo: 20,
      unidad: 'páginas',
      estado: 'Activo',
      metaId: META_DEMO_ID,
      fechaInicioCiclo: inicioCiclo,
      registros: [
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000001',
          cantidad: 25,
          fecha: inicioDelDiaUtc(-2, ahora),
          unidad: 'páginas',
        },
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000002',
          cantidad: 20,
          fecha: inicioDelDiaUtc(-1, ahora),
          unidad: 'páginas',
        },
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000005',
          cantidad: 22,
          fecha: inicioDelDiaUtc(-3, ahora),
          unidad: 'páginas',
        },
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000006',
          cantidad: 18,
          fecha: inicioDelDiaUtc(-4, ahora),
          unidad: 'páginas',
        },
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000003',
          cantidad: 8,
          fecha: inicioDelDiaUtc(0, ahora),
          unidad: 'páginas',
        },
      ],
    },
    {
      id: '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e02',
      usuarioId: estandar,
      nombre: 'Correr',
      periodicidad: 'Semanal',
      cantidadObjetivo: 15,
      unidad: 'km',
      estado: 'Planificado',
      metaId: META_DEMO_ID,
      fechaInicioCiclo: inicioCiclo,
      registros: [],
    },
    {
      id: '7c1d2e3f-4a5b-4c6d-8e7f-9a0b1c2d3e03',
      usuarioId: estandar,
      nombre: 'Meditar',
      periodicidad: 'Diaria',
      cantidadObjetivo: 10,
      unidad: 'minutos',
      estado: 'Pausado',
      metaId: META_DEMO_ID,
      fechaInicioCiclo: inicioCiclo,
      registros: [
        {
          id: '9e8d7c6b-5a49-4382-9100-000000000004',
          cantidad: 10,
          fecha: inicioDelDiaUtc(-3, ahora),
          unidad: 'minutos',
        },
      ],
    },
  ]

  // Mis planes: en las pruebas nadie sigue un plan hasta que una prueba llama a sembrarPlanDemo() (al
  // final de esta función se siembra solo fuera de las pruebas). Ana ya tiene avances en sus hábitos
  // sueltos, así que su primer logro está ganado y celebrado.
  db.planesUsuario = []
  db.logros = [logroVisto(estandar, 'PrimerAvance', 9, ahora)]

  db.solicitudes = [
    {
      id: '5a4b3c2d-1e0f-4a9b-8c7d-6e5f4a3b2c01',
      solicitante: 'otro@demo.com',
      permisoSolicitado: 'Habits.AdvancedReconfig',
      estado: 'Pendiente',
      aprobador: null,
      fechaCreacion: new Date(ahora - 2 * DIA).toISOString(),
      fechaResolucion: null,
      motivoRechazo: null,
    },
  ]

  db.metas = new Set([META_DEMO_ID])
  db.tokensRevocados = new Set()
  db.refresh = null
  db.sesiones = []
  db.vinculosGoogle = new Map()

  // Ana y Luis ya completaron el onboarding. La cuenta sin activar y las que se registren
  // empiezan sin perfil, así que pasan por /bienvenida.
  const consentimiento = (marketing: boolean): ConsentimientosDto => ({
    terminosVersion: 'v2.1',
    privacidadVersion: 'v1.4',
    aceptadoEn: new Date(ahora - 17 * DIA).toISOString(),
    datosSalud: true,
    marketing,
    compartirCumplimiento: false,
    estadisticasAnonimas: true,
  })
  db.perfiles = new Map([
    [
      estandar,
      {
        perfilLaboral: 'OficinaDiurna',
        zonaHoraria: 'America/Santo_Domingo',
        avisoSaludAceptado: true,
        horaEntradaTrabajo: '08:00:00',
        horaSalidaTrabajo: '17:00:00',
        horaDespertar: '06:30:00',
        horaDormir: '22:30:00',
        edad: 29,
        sexo: 'Mujer',
        alturaCm: 165,
        pesoKg: 68.4,
        actividad: 'Ligera',
        diasPorSemana: 3,
        minutosPorSesion: 30,
        metaSlug: 'primeros-5-km',
        // Semana 3 de 8 del plan.
        inicioPlan: inicioDelDiaUtc(-17, ahora),
        actualizadoEn: new Date(ahora - 17 * DIA).toISOString(),
      },
    ],
    [
      db.usuarios[2].id,
      {
        perfilLaboral: 'TurnoNocturno',
        zonaHoraria: 'America/Santo_Domingo',
        avisoSaludAceptado: true,
        horaEntradaTrabajo: '22:00:00',
        horaSalidaTrabajo: '06:00:00',
        horaDespertar: null,
        horaDormir: null,
        edad: 34,
        sexo: null,
        alturaCm: 178,
        pesoKg: 81,
        actividad: 'Moderada',
        diasPorSemana: 4,
        minutosPorSesion: 45,
        metaSlug: null,
        inicioPlan: null,
        actualizadoEn: new Date(ahora - 20 * DIA).toISOString(),
      },
    ],
  ])
  db.cuestionarios = new Map()
  db.consentimientos = new Map([
    [estandar, consentimiento(false)],
    [db.usuarios[2].id, consentimiento(true)],
  ])

  // Progreso de Ana (datos de ejemplo): un peso por semana durante 10 semanas y medidas al
  // inicio y hoy.
  const pesosAna = [71, 70.8, 70.5, 70.1, 69.9, 69.6, 69.4, 69.1, 68.8, 68.4]
  db.pesos = new Map([
    [
      estandar,
      {
        objetivoKg: 66,
        registros: pesosAna.map((pesoKg, i) => ({
          id: `bb000000-0000-4000-8000-${String(i).padStart(12, '0')}`,
          fecha: inicioDelDiaUtc(-7 * (pesosAna.length - 1 - i), ahora),
          pesoKg,
        })),
      },
    ],
  ])
  const medida = (n: number, dias: number, zona: MedicionDto['zona'], valor: number) => ({
    id: `cc000000-0000-4000-8000-${String(n).padStart(12, '0')}`,
    fecha: inicioDelDiaUtc(dias, ahora),
    zona,
    valor,
  })
  db.mediciones = new Map([
    [
      estandar,
      [
        medida(1, -63, 'Cintura', 82),
        medida(2, -63, 'Pecho', 96),
        medida(3, -63, 'Brazo', 29),
        medida(4, -63, 'Grasa', 27),
        medida(5, -1, 'Cintura', 78),
        medida(6, -1, 'Pecho', 95),
        medida(7, -1, 'Brazo', 29.5),
        medida(8, -1, 'Grasa', 25),
      ],
    ],
  ])

  // Auditoría de ejemplo: los últimos 40 días, con el historial que alimenta las métricas.
  const evento = (
    n: number,
    dias: number,
    actor: string,
    accion: EventoAuditoriaDto['accion'],
    entidad: string,
    extra: Partial<EventoAuditoriaDto> = {},
  ): EventoAuditoriaDto => ({
    id: `dd000000-0000-4000-8000-${String(n).padStart(12, '0')}`,
    fecha: new Date(ahora - dias * DIA - n * 60_000).toISOString(),
    actor,
    accion,
    entidad,
    entidadId: null,
    valorAnterior: null,
    valorNuevo: null,
    origen: 'Web',
    ...extra,
  })
  db.auditoria = [
    evento(1, 40, 'estandar@demo.com', 'Registro', 'Usuario'),
    evento(2, 38, 'otro@demo.com', 'Registro', 'Usuario'),
    evento(3, 30, 'admin@demo.com', 'InicioSesion', 'Sesión'),
    evento(4, 33, 'otro@demo.com', 'InicioSesion', 'Sesión'),
    evento(5, 9, 'estandar@demo.com', 'InicioSesion', 'Sesión'),
    evento(6, 6, 'admin@demo.com', 'CambioRol', 'Usuario', {
      entidadId: db.usuarios[2].id,
      valorAnterior: 'Administrador',
      valorNuevo: 'Estandar',
    }),
    evento(7, 3, 'estandar@demo.com', 'InicioSesion', 'Sesión'),
    evento(8, 2, 'admin@demo.com', 'InicioSesion', 'Sesión'),
    evento(9, 4, 'estandar@demo.com', 'DescargaDatos', 'Cuenta'),
  ]
  db.bajas = new Map()

  // Ana ya tiene otras sesiones abiertas (otros dispositivos) para poder probar «Sesiones activas».
  db.sesiones = [
    {
      id: '5e5510a0-0000-4000-8000-000000000001',
      usuarioId: estandar,
      dispositivo: 'Firefox en Windows',
      creadaEn: new Date(ahora - 5 * DIA).toISOString(),
      ultimoUsoEn: new Date(ahora - 2 * DIA).toISOString(),
    },
    {
      id: '5e5510a0-0000-4000-8000-000000000002',
      usuarioId: estandar,
      dispositivo: 'Safari en iPhone',
      creadaEn: new Date(ahora - 20 * DIA).toISOString(),
      ultimoUsoEn: new Date(ahora - 3 * 60 * 60 * 1000).toISOString(),
    },
  ]

  // La demo del navegador arranca con el plan de ejemplo; las pruebas lo piden con sembrarPlanDemo().
  if (import.meta.env.MODE !== 'test') sembrarPlanDemo(ahora)
}

reiniciarDb()
