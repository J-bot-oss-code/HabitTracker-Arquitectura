import { beforeEach, describe, expect, it } from 'vitest'
import { iniciarSesionComo } from '@/test/render'
import { PLAN_DEMO_ID, sembrarPlanDemo, sembrarPoblacionDemo } from './db'

// Un Básico con guía semanal: es el plan que cubre los campos de la guía en el catálogo y en Mis planes.
const PLAN_CON_GUIA = 'caminar-para-empezar'

// La forma de cada respuesta de la API simulada debe ser EXACTAMENTE la de Host/WebApi: los mismos campos que
// el record de la capa Application (System.Text.Json los pasa a camelCase), ni uno más ni uno menos. Un mock
// con otra forma esconde un fallo que solo aparece contra el backend real (por ejemplo, un `eventos.map` sobre
// la página `{ total, pagina, tamano, registros }` de GET /api/audit). Las listas de campos de abajo se copian
// de los records de C#; si un DTO del backend cambia, se actualiza aquí primero.
const url = (ruta: string) => `${window.location.origin}${ruta}`

async function llamar(ruta: string, token: string, metodo = 'GET', body?: unknown) {
  const esFormulario = body instanceof FormData
  return fetch(url(ruta), {
    method: metodo,
    headers: {
      ...(esFormulario || body === undefined ? {} : { 'Content-Type': 'application/json' }),
      Authorization: `Bearer ${token}`,
    },
    body: esFormulario ? body : body === undefined ? undefined : JSON.stringify(body),
  })
}

async function json<T = Record<string, unknown>>(
  ruta: string,
  token: string,
  metodo = 'GET',
  body?: unknown,
): Promise<T> {
  const r = await llamar(ruta, token, metodo, body)
  expect(r.ok, `${metodo} ${ruta} -> ${r.status}`).toBe(true)
  return (await r.json()) as T
}

const campos = (valor: unknown): string[] => Object.keys(valor as object).sort()

// Comprueba que `valor` tiene exactamente estos campos.
function forma(valor: unknown, esperados: string[], etiqueta: string) {
  expect(valor, etiqueta).toBeTypeOf('object')
  expect(valor, etiqueta).not.toBeNull()
  expect(campos(valor), etiqueta).toEqual([...esperados].sort())
}

// Comprueba la forma de cada elemento de una lista, que no debe venir vacía (si no, la prueba no probaría nada).
function formaLista(lista: unknown, esperados: string[], etiqueta: string) {
  expect(Array.isArray(lista), `${etiqueta} debe ser una lista`).toBe(true)
  expect((lista as unknown[]).length, `${etiqueta} no debe estar vacía`).toBeGreaterThan(0)
  for (const elemento of lista as unknown[]) forma(elemento, esperados, etiqueta)
}

const USUARIO = [
  'id',
  'nombre',
  'correo',
  'rol',
  'activo',
  'token',
  'requiereAceptarDocumentos',
  'bajaProgramadaPara',
]
const DOCUMENTO = [
  'id',
  'nombre',
  'tipo',
  'tamanoBytes',
  'fechaSubidaUtc',
  'usuarioId',
  'correoUsuario',
]
const HABITO = [
  'id',
  'nombre',
  'periodicidad',
  'cantidadObjetivo',
  'unidad',
  'estado',
  'cumpleMetaFinal',
  'fechaInicioCiclo',
  'registros',
  'planUsuarioId',
  'descripcion',
  'versionMinima',
  'ancla',
]
const REGISTRO_AVANCE = ['id', 'cantidad', 'fecha', 'unidad', 'esVersionMinima']
const CUMPLIMIENTO = [
  'diasCompletos',
  'diasMinimos',
  'diasElegibles',
  'porcentajeCompleto',
  'porcentajeConMinimo',
]
const SUGERENCIA = ['opciones', 'diasSinRegistro']
const PLAN_RESUMEN = [
  'slug',
  'nombre',
  'resumen',
  'area',
  'nivel',
  'intensidad',
  'semanas',
  'sexo',
  'edadMinima',
  'edadMaxima',
  'perfilesLaborales',
  'beneficios',
  'requiereCuestionario',
]
const NOTIFICACION = ['id', 'tipo', 'titulo', 'mensaje', 'creadaEnUtc', 'leida', 'leidaEnUtc']
// SugerenciaDto y SugerenciaAdminDto (SugerenciasService.cs): el del Administrador añade usuarioId.
const SUGERENCIA_DTO = ['id', 'tipo', 'texto', 'creadaEn', 'estado']
const SUGERENCIA_ADMIN = ['id', 'usuarioId', 'tipo', 'texto', 'creadaEn', 'estado']
// MetricasProductoDto y PlanIniciadoDto (ReportesDtos.cs).
const METRICAS_PRODUCTO = [
  'generadoEn',
  'cohorteMinima',
  'cuentasNuevas7d',
  'eliminacionesEnCurso',
  'sugerenciasNuevas',
  'registrosPorCorreo7d',
  'usuariosActivos30d',
  'retencion30d',
  'planesMasIniciados',
]
const PLAN_INICIADO = ['slug', 'cantidad']

let ana: string
let admin: string

beforeEach(() => {
  ana = iniciarSesionComo('estandar')
  admin = iniciarSesionComo('admin')
  sembrarPlanDemo()
})

describe('forma de las respuestas de Control de Acceso', () => {
  it('login y lista de usuarios: UsuarioResponseDto', async () => {
    const r = await fetch(url('/api/auth/login'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'estandar@demo.com', password: 'Demo1234' }),
    })
    forma(await r.json(), USUARIO, 'POST /api/auth/login')
    formaLista(await json('/api/admin/usuarios', admin), USUARIO, 'GET /api/admin/usuarios')
  })

  it('la lista de usuarios no calcula lo que el servidor deja por defecto', async () => {
    const lista = await json<Record<string, unknown>[]>('/api/admin/usuarios', admin)
    for (const u of lista) {
      expect(u.token).toBeNull()
      expect(u.requiereAceptarDocumentos).toBe(false)
      expect(u.bajaProgramadaPara).toBeNull()
    }
  })

  it('me, vínculos y sesiones', async () => {
    forma(
      await json('/api/auth/me', ana),
      ['id', 'nombre', 'correo', 'rol', 'requiereAceptarDocumentos'],
      'GET /api/auth/me',
    )
    const vinculos = await json<Record<string, unknown>>('/api/auth/vinculos', ana)
    forma(vinculos, ['tienePassword', 'vinculos'], 'GET /api/auth/vinculos')
    formaLista(
      await json('/api/auth/sesiones', ana),
      ['id', 'dispositivo', 'creadaEn', 'ultimoUsoEn', 'actual'],
      'GET /api/auth/sesiones',
    )
  })

  it('un ProblemDetails del middleware lleva title, status, detail e instance (y codigo), sin type', async () => {
    const r = await llamar('/api/permissions/11111111-2222-4333-8444-555555555555', ana)
    expect(r.status).toBe(404)
    forma(await r.json(), ['title', 'status', 'detail', 'instance', 'codigo'], 'ProblemDetails')
  })
})

describe('forma de las respuestas de administración, auditoría y reportes', () => {
  it('GET /api/audit es una página { total, pagina, tamano, registros }, no una lista', async () => {
    const pagina = await json<Record<string, unknown>>('/api/audit', admin)
    forma(pagina, ['total', 'pagina', 'tamano', 'registros'], 'GET /api/audit')
    formaLista(
      pagina.registros,
      [
        'id',
        'fecha',
        'usuarioId',
        'actor',
        'accion',
        'entidad',
        'entidadId',
        'valorAnterior',
        'valorNuevo',
      ],
      'registros de auditoría',
    )
    expect(pagina).toMatchObject({ pagina: 1, tamano: 50 })
  })

  it('GET /api/me/activity es la misma página que /api/audit, solo con lo de la sesión', async () => {
    const pagina = await json<Record<string, unknown>>('/api/me/activity?dias=30', ana)
    forma(pagina, ['total', 'pagina', 'tamano', 'registros'], 'GET /api/me/activity')
    formaLista(
      pagina.registros,
      [
        'id',
        'fecha',
        'usuarioId',
        'actor',
        'accion',
        'entidad',
        'entidadId',
        'valorAnterior',
        'valorNuevo',
      ],
      'registros de mi actividad',
    )
    expect(pagina).toMatchObject({ pagina: 1, tamano: 50 })
    // El Administrador también la tiene (cualquier rol con sesión), pero solo la suya.
    const deAdmin = await json<{ registros: { actor: string }[] }>(
      '/api/me/activity?dias=30',
      admin,
    )
    expect(deAdmin.registros.length).toBeGreaterThan(0)
    expect(deAdmin.registros.every((r) => r.actor === 'admin@demo.com')).toBe(true)
  })

  it('cola de correos: CorreoColaDto y ResultadoColaDto', async () => {
    // Una solicitud de permiso avisa a los Administradores y deja un correo Pendiente en la cola.
    await json('/api/permissions', ana, 'POST', { permisoSolicitado: 'Habits.AdvancedReconfig' })
    formaLista(
      await json('/api/admin/email-queue', admin),
      [
        'id',
        'destinatario',
        'asunto',
        'estado',
        'intentos',
        'fechaCreacion',
        'fechaUltimoIntento',
        'fechaEnvio',
        'ultimoError',
      ],
      'GET /api/admin/email-queue',
    )
    forma(
      await json('/api/admin/email-queue/process', admin, 'POST'),
      ['enviados', 'fallidos', 'recuperados'],
      'POST /api/admin/email-queue/process',
    )
  })

  it('solicitudes de permiso: SolicitudPermisoDto, { id } y { mensaje }', async () => {
    const solicitudes = ['id', 'solicitante', 'permisoSolicitado', 'estado', 'aprobador']
    const lista = await json<Record<string, unknown>[]>('/api/permissions', admin)
    formaLista(
      lista,
      [...solicitudes, 'fechaCreacion', 'fechaResolucion', 'motivoRechazo'],
      'GET /api/permissions',
    )
    const creada = await json<{ id: string }>('/api/permissions', ana, 'POST', {
      permisoSolicitado: 'Habits.Otra',
    })
    forma(creada, ['id'], 'POST /api/permissions')
    forma(
      await json(`/api/permissions/${creada.id}`, ana),
      [...solicitudes, 'fechaCreacion', 'fechaResolucion', 'motivoRechazo'],
      'GET /api/permissions/{id}',
    )
    forma(
      await json(`/api/permissions/${creada.id}/approve`, admin, 'POST'),
      ['mensaje'],
      'POST /api/permissions/{id}/approve',
    )
  })

  it('reportes: ReportePermisosDto y ReporteConstanciaDto', async () => {
    const hoy = new Date()
    const desde = new Date(hoy.getTime() - 30 * 86_400_000).toISOString().slice(0, 10)
    const hasta = new Date(hoy.getTime() + 86_400_000).toISOString().slice(0, 10)
    const rango = `desde=${desde}&hasta=${hasta}`
    const permisos = await json<Record<string, unknown>>(`/api/reports/permissions?${rango}`, admin)
    forma(permisos, ['desde', 'hasta', 'alcance', 'filas', 'total'], 'GET /api/reports/permissions')
    formaLista(permisos.filas, ['estado', 'cantidad'], 'filas de permisos')
    const constancia = await json<Record<string, unknown>>(`/api/reports/adherence?${rango}`, ana)
    forma(
      constancia,
      ['desde', 'hasta', 'alcance', 'pregunta', 'filas'],
      'GET /api/reports/adherence',
    )
    formaLista(
      constancia.filas,
      [
        'plan',
        'personas',
        'diasConAvance',
        'diasSoloMinimo',
        'diasEnRango',
        'porcentajeConstancia',
      ],
      'filas de constancia',
    )
  })
})

describe('forma de las métricas del Administrador', () => {
  it('MetricasProductoDto con la cohorte pequeña: lo que cuenta personas llega null y sin planes', async () => {
    const m = await json<Record<string, unknown>>('/api/admin/metrics', admin)
    forma(m, METRICAS_PRODUCTO, 'GET /api/admin/metrics')
    expect(m).toMatchObject({
      cohorteMinima: 5,
      usuariosActivos30d: null,
      retencion30d: null,
      planesMasIniciados: [],
    })
    expect(typeof m.generadoEn).toBe('string')
  })

  it('MetricasProductoDto con la cohorte completa y PlanIniciadoDto', async () => {
    sembrarPoblacionDemo()
    const m = await json<Record<string, unknown>>('/api/admin/metrics', admin)
    forma(m, METRICAS_PRODUCTO, 'GET /api/admin/metrics')
    formaLista(m.planesMasIniciados, PLAN_INICIADO, 'planesMasIniciados')
    expect(typeof m.usuariosActivos30d).toBe('number')
    expect(Number.isInteger(m.retencion30d)).toBe(true)
  })
})

describe('forma de las respuestas de notificaciones', () => {
  it('NotificacionDto y PreferenciasNotificacionesDto', async () => {
    await json('/api/permissions', ana, 'POST', { permisoSolicitado: 'Habits.AdvancedReconfig' })
    formaLista(await json('/api/notifications', admin), NOTIFICACION, 'GET /api/notifications')
    forma(
      await json('/api/notifications/preferences', ana),
      ['correoActivo'],
      'GET /api/notifications/preferences',
    )
    forma(
      await json('/api/notifications/preferences', ana, 'PUT', { correoActivo: false }),
      ['correoActivo'],
      'PUT /api/notifications/preferences',
    )
  })
})

describe('forma de las respuestas de quejas y mejoras', () => {
  it('SugerenciaDto, PaginaSugerenciasDto y SugerenciaAdminDto', async () => {
    const enviada = await llamar('/api/feedback', ana, 'POST', {
      tipo: 'Queja',
      texto: 'La lista de planes tarda en cargar.',
    })
    expect(enviada.status).toBe(201)
    const creada = (await enviada.json()) as { id: string }
    forma(creada, SUGERENCIA_DTO, 'POST /api/feedback')
    formaLista(await json('/api/feedback', ana), SUGERENCIA_DTO, 'GET /api/feedback')

    const pagina = await json<Record<string, unknown>>('/api/admin/feedback', admin)
    forma(pagina, ['total', 'pagina', 'tamano', 'nuevas', 'sugerencias'], 'GET /api/admin/feedback')
    formaLista(pagina.sugerencias, SUGERENCIA_ADMIN, 'sugerencias de la bandeja')
    expect(pagina).toMatchObject({ total: 1, pagina: 1, tamano: 20, nuevas: 1 })

    forma(
      await json(`/api/admin/feedback/${creada.id}/status`, admin, 'POST', { estado: 'Leida' }),
      SUGERENCIA_ADMIN,
      'POST /api/admin/feedback/{id}/status',
    )
  })
})

describe('forma de las respuestas de documentos', () => {
  it('DocumentoDto, VinculoDocumentoDto y AdjuntoPlanDto', async () => {
    const formulario = new FormData()
    formulario.append('archivo', new File(['hola'], 'nota.txt', { type: 'text/plain' }))
    const subido = await json('/api/documents', ana, 'POST', formulario)
    forma(subido, DOCUMENTO, 'POST /api/documents')
    formaLista(await json('/api/documents', ana), DOCUMENTO, 'GET /api/documents')

    const adjuntar = await llamar(`/api/my-plans/${PLAN_DEMO_ID}/attachments`, ana, 'POST', {
      documentoId: (subido as { id: string }).id,
    })
    expect(adjuntar.status).toBe(204)
    formaLista(
      await json('/api/my-plans/attachments', ana),
      ['documentoId', 'planId', 'planNombre', 'enCurso'],
      'GET /api/my-plans/attachments',
    )
    formaLista(
      await json(`/api/my-plans/${PLAN_DEMO_ID}/attachments`, ana),
      ['documentoId', 'nombre', 'tipo', 'tamanoBytes', 'fechaSubidaUtc', 'adjuntadoEnUtc'],
      'GET /api/my-plans/{id}/attachments',
    )
  })
})

describe('forma de las respuestas de hábitos, planes y progreso', () => {
  it('HabitoDto y RegistroAvanceDto', async () => {
    const lista = await json<Record<string, unknown>[]>('/api/habits', ana)
    formaLista(lista, HABITO, 'GET /api/habits')
    const conAvances = lista.find((h) => (h.registros as unknown[]).length > 0)!
    formaLista(conAvances.registros, REGISTRO_AVANCE, 'registros de avance')
    forma(await json(`/api/habits/${conAvances.id}`, ana), HABITO, 'GET /api/habits/{id}')
    const creado = await json('/api/habits', ana, 'POST', {
      nombre: 'Leer',
      periodicidad: 'Diaria',
      cantidadObjetivo: 1,
      unidad: 'capítulo',
    })
    forma(creado, ['id'], 'POST /api/habits')
  })

  it('PlanUsuarioDto con su progreso, su sugerencia y su guía', async () => {
    const inscrito = await json<{ id: string }>('/api/my-plans', ana, 'POST', {
      slug: PLAN_CON_GUIA,
    })
    const plan = await json<Record<string, unknown>>(`/api/my-plans/${inscrito.id}`, ana)
    forma(
      plan,
      [
        'id',
        'slug',
        'nombre',
        'origen',
        'estado',
        'motivoPausa',
        'semanas',
        'semanasTotales',
        'extensiones',
        'fechaInicio',
        'fechaFinPrevista',
        'modoSuaveHasta',
        'horaRecordatorio',
        'recordatoriosActivos',
        'habitos',
        'progreso',
        'sugerencia',
        'guia',
      ],
      'GET /api/my-plans/{id}',
    )
    formaLista(
      plan.habitos,
      [
        'habitoId',
        'nombre',
        'descripcion',
        'periodicidad',
        'cantidadObjetivo',
        'unidad',
        'estado',
        'versionMinima',
        'ancla',
        'horaSugerida',
      ],
      'hábitos del plan',
    )
    const progreso = plan.progreso as Record<string, unknown>
    forma(
      progreso,
      [
        'racha',
        'rachaMasLarga',
        'siete',
        'treinta',
        'semanaActual',
        'semanasTotales',
        'diasRestantes',
        'diasCumplidos',
        'diasSinRegistroSeguidos',
        'cumplidoHoy',
        'semanasCelebradas',
        'unidad',
      ],
      'progreso del plan',
    )
    forma(progreso.siete, CUMPLIMIENTO, 'cumplimiento de 7 días')
    forma(progreso.treinta, CUMPLIMIENTO, 'cumplimiento de 30 días')
    if (plan.sugerencia !== null) forma(plan.sugerencia, SUGERENCIA, 'sugerencia')
    formaLista(plan.guia, ['semana', 'texto', 'tipExtra'], 'guía semanal del plan')
    formaLista(await json('/api/my-plans', ana), Object.keys(plan), 'GET /api/my-plans')
  })

  it('HoyDto y ResumenProgresoDto', async () => {
    const hoy = await json<Record<string, unknown>>('/api/today', ana)
    forma(
      hoy,
      ['fecha', 'tareas', 'planes', 'mensaje', 'celebracionSemana', 'tipExtra', 'logrosNuevos'],
      'GET /api/today',
    )
    formaLista(
      hoy.tareas,
      [
        'habitoId',
        'planId',
        'planNombre',
        'nombre',
        'instrucciones',
        'periodicidad',
        'cantidadObjetivo',
        'unidad',
        'ancla',
        'horaSugerida',
        'versionMinima',
        'estado',
        'cantidadPeriodo',
        'restante',
      ],
      'tareas de hoy',
    )
    formaLista(
      hoy.planes,
      [
        'planId',
        'nombre',
        'semanaActual',
        'semanasTotales',
        'diasRestantes',
        'racha',
        'sugerencia',
      ],
      'planes de hoy',
    )
    expect(hoy.fecha).toMatch(/^\d{4}-\d{2}-\d{2}$/)

    const resumen = await json<Record<string, unknown>>('/api/progress/summary', ana)
    forma(
      resumen,
      ['racha', 'rachaMasLarga', 'logros', 'mes', 'mensaje', 'planesEnCurso'],
      'GET /api/progress/summary',
    )
    formaLista(resumen.logros, ['clave', 'obtenidoEn', 'visto'], 'logros')
    forma(resumen.mes, ['diasCumplidos', 'habitosMantenidos', 'regresos'], 'tu mes en números')
  })

  it('catálogo de planes: PlanResumenDto, PlanDetalleDto y RecomendacionesDto', async () => {
    const lista = await json<Record<string, unknown>[]>('/api/plans', ana)
    formaLista(lista, PLAN_RESUMEN, 'GET /api/plans')
    const detalle = await json<Record<string, unknown>>(`/api/plans/${PLAN_CON_GUIA}`, ana)
    forma(
      detalle,
      [
        'slug',
        'nombre',
        'resumen',
        'descripcion',
        'area',
        'nivel',
        'intensidad',
        'semanas',
        'sexo',
        'edadMinima',
        'edadMaxima',
        'perfilesLaborales',
        'lugar',
        'impacto',
        'zonasCorporales',
        'contraindicaciones',
        'alergenos',
        'beneficios',
        'avisoSeguridad',
        'requiereCuestionario',
        'tareas',
        'guiaSemanal',
      ],
      'GET /api/plans/{slug}',
    )
    formaLista(
      detalle.tareas,
      [
        'orden',
        'nombre',
        'instrucciones',
        'periodicidad',
        'cantidad',
        'unidad',
        'versionMinima',
        'anclaSugerida',
      ],
      'tareas del plan',
    )
    formaLista(detalle.guiaSemanal, ['semana', 'texto'], 'guía del detalle público')

    const recomendaciones = await json<Record<string, unknown>>('/api/plans/recommended', ana)
    forma(
      recomendaciones,
      ['recomendados', 'noAptos', 'personalizadoConSalud'],
      'GET /api/plans/recommended',
    )
    for (const r of recomendaciones.recomendados as Record<string, unknown>[]) {
      forma(r, ['plan', 'motivos', 'advertencias'], 'plan recomendado')
      forma(r.plan, PLAN_RESUMEN, 'plan recomendado › plan')
    }
    for (const n of recomendaciones.noAptos as Record<string, unknown>[]) {
      forma(n, ['plan', 'razones'], 'plan no apto')
      formaLista(n.razones, ['motivo', 'mensaje'], 'razones de un plan no apto')
    }
  })

  it('peso y medidas: PesosDto, PesoDto y MedicionDto', async () => {
    const pesos = await json<Record<string, unknown>>('/api/progress/weights', ana)
    forma(pesos, ['objetivoKg', 'registros'], 'GET /api/progress/weights')
    formaLista(pesos.registros, ['id', 'fecha', 'pesoKg'], 'registros de peso')
    formaLista(
      await json('/api/progress/measurements', ana),
      ['id', 'fecha', 'zona', 'valor'],
      'GET /api/progress/measurements',
    )
  })
})

describe('forma de las respuestas de perfil y privacidad', () => {
  it('PerfilDto', async () => {
    forma(
      await json('/api/profile', ana),
      [
        'nombre',
        'perfilLaboral',
        'zonaHoraria',
        'avisoSaludAceptado',
        'horaEntradaTrabajo',
        'horaSalidaTrabajo',
        'horaDespertar',
        'horaDormir',
        'edad',
        'sexo',
        'alturaCm',
        'pesoKg',
        'actividad',
        'diasPorSemana',
        'minutosPorSesion',
        'actualizadoEn',
      ],
      'GET /api/profile',
    )
  })

  it('CuestionarioSaludDto con sus tres bloques y la evaluación', async () => {
    const consentimientos = await json<Record<string, unknown>>('/api/privacy/consents', ana)
    forma(
      consentimientos,
      [
        'terminosVersion',
        'privacidadVersion',
        'aceptadoEn',
        'datosSalud',
        'marketing',
        'compartirCumplimiento',
        'estadisticasAnonimas',
      ],
      'GET /api/privacy/consents',
    )
    await json('/api/privacy/consents', ana, 'PUT', { ...consentimientos, datosSalud: true })
    await json('/api/profile/health/objetivos', ana, 'PUT', {
      objetivos: ['MasEnergia'],
      zonas: [],
      experiencia: 'Ninguna',
      lugar: 'CasaSinEquipo',
    })
    await json('/api/profile/health/seguridad', ana, 'PUT', {
      afeccionCardiacaOPresionAlta: false,
      dolorDePecho: false,
      mareosOPerdidaDeConocimiento: false,
      otraEnfermedadCronica: false,
      medicacionCronica: false,
      problemaOseoOArticular: false,
      soloActividadSupervisada: false,
    })
    const salud = await json<Record<string, unknown>>(
      '/api/profile/health/condiciones',
      ana,
      'PUT',
      {
        condiciones: ['Ninguna'],
        alergias: ['Ninguna'],
      },
    )
    forma(
      salud,
      [
        'version',
        'objetivos',
        'objetivosRespondidoEn',
        'seguridad',
        'seguridadRespondidoEn',
        'condiciones',
        'condicionesRespondidoEn',
        'evaluacion',
        'usaDatosDeSalud',
      ],
      'PUT /api/profile/health/condiciones',
    )
    forma(salud.objetivos, ['objetivos', 'zonas', 'experiencia', 'lugar'], 'bloque de objetivos')
    forma(
      salud.seguridad,
      [
        'afeccionCardiacaOPresionAlta',
        'dolorDePecho',
        'mareosOPerdidaDeConocimiento',
        'otraEnfermedadCronica',
        'medicacionCronica',
        'problemaOseoOArticular',
        'soloActividadSupervisada',
      ],
      'bloque de seguridad',
    )
    forma(salud.condiciones, ['condiciones', 'alergias'], 'bloque de condiciones')
    const evaluacion = salud.evaluacion as Record<string, unknown>
    forma(
      evaluacion,
      ['nivel', 'restricciones', 'alergenosAEvitar', 'completitud'],
      'evaluación de salud',
    )
    forma(
      evaluacion.completitud,
      ['objetivos', 'seguridad', 'condiciones'],
      'completitud del cuestionario',
    )
  })

  it('baja de cuenta y descarga de datos', async () => {
    const formulario = new FormData()
    formulario.append('archivo', new File(['hola'], 'nota.txt', { type: 'text/plain' }))
    await json('/api/documents', ana, 'POST', formulario)
    await json('/api/permissions', ana, 'POST', { permisoSolicitado: 'Habits.AdvancedReconfig' })

    const baja = await llamar('/api/privacy/deletion-request', ana, 'POST', {
      modo: 'eliminar',
      password: 'Demo1234',
    })
    expect(baja.status).toBe(202)
    forma(await baja.json(), ['modo', 'borradoEn'], 'POST /api/privacy/deletion-request')
    forma(
      await json('/api/privacy/deletion-request', iniciarSesionComoDeNuevo()),
      ['borradoEn'],
      'GET /api/privacy/deletion-request',
    )
  })

  it('ExportacionDatosDto con una sección por módulo', async () => {
    const formulario = new FormData()
    formulario.append('archivo', new File(['hola'], 'nota.txt', { type: 'text/plain' }))
    await json('/api/documents', ana, 'POST', formulario)
    await json('/api/permissions', ana, 'POST', { permisoSolicitado: 'Habits.AdvancedReconfig' })
    await json('/api/feedback', ana, 'POST', {
      tipo: 'Mejora',
      texto: 'Me gustaría elegir la hora de cada recordatorio.',
    })

    const datos = await json<Record<string, unknown>>('/api/privacy/export', ana)
    forma(
      datos,
      ['generadoEn', 'cuenta', 'consentimientos', 'vinculos', 'sesiones', 'modulos'],
      'GET /api/privacy/export',
    )
    forma(
      datos.cuenta,
      [
        'id',
        'nombre',
        'correo',
        'rol',
        'activo',
        'fechaCreacion',
        'tienePassword',
        'desactivadaPorUsuarioEn',
        'bajaProgramadaPara',
      ],
      'cuenta exportada',
    )
    formaLista(
      datos.consentimientos,
      [
        'terminosVersion',
        'privacidadVersion',
        'datosSalud',
        'marketing',
        'compartirCumplimiento',
        'estadisticasAnonimas',
        'aceptadoEn',
        'actualizadoEn',
        'origen',
      ],
      'consentimientos exportados',
    )
    formaLista(
      datos.sesiones,
      ['id', 'dispositivo', 'ip', 'creadaEn', 'ultimoUsoEn', 'expiraEn'],
      'sesiones exportadas',
    )

    const modulos = datos.modulos as Record<string, unknown>
    expect(campos(modulos)).toEqual(
      [
        'habitos',
        'permisos',
        'documentos',
        'planes',
        'notificaciones',
        'sugerencias',
        'perfilYSalud',
      ].sort(),
    )
    formaLista(modulos.habitos, HABITO, 'habitos exportados')
    formaLista(
      modulos.permisos,
      [
        'id',
        'solicitante',
        'permisoSolicitado',
        'estado',
        'aprobador',
        'fechaCreacion',
        'fechaResolucion',
        'motivoRechazo',
      ],
      'permisos exportados',
    )
    formaLista(modulos.documentos, DOCUMENTO, 'documentos exportados')
    const planes = modulos.planes as Record<string, unknown>
    forma(planes, ['planes', 'logros'], 'sección planes')
    formaLista(
      planes.planes,
      [
        'id',
        'slug',
        'nombre',
        'estado',
        'fechaInicio',
        'fechaFinPrevista',
        'extensiones',
        'modoSuaveHasta',
        'horaRecordatorio',
        'recordatoriosActivos',
        'pausas',
        'archivosVinculados',
      ],
      'planes exportados',
    )
    formaLista(planes.logros, ['clave', 'obtenidoEn', 'visto'], 'logros exportados')
    const notificaciones = modulos.notificaciones as Record<string, unknown>
    forma(notificaciones, ['preferencias', 'notificaciones'], 'sección notificaciones')
    forma(notificaciones.preferencias, ['correoActivo'], 'preferencias exportadas')
    formaLista(modulos.sugerencias, SUGERENCIA_DTO, 'sugerencias exportadas')
    const perfilYSalud = modulos.perfilYSalud as Record<string, unknown>
    forma(perfilYSalud, ['perfil', 'cuestionario', 'pesos', 'mediciones'], 'sección perfilYSalud')
  })
})

// El token del "estandar" sigue valiendo tras pedir la baja (la API simulada solo corta las sesiones de la
// persona al llamar a un endpoint que las revoca): se emite otro para consultar la fecha del borrado.
function iniciarSesionComoDeNuevo() {
  return iniciarSesionComo('estandar')
}
