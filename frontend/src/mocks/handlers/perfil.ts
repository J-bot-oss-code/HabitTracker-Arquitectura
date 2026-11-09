import { http, HttpResponse } from 'msw'
import { LIMITES_PLAN } from '@/features/perfil/schemas'
import {
  ACTIVIDADES_PERFIL,
  ALERGIAS,
  CONDICIONES,
  EXPERIENCIAS,
  LUGARES,
  MAXIMO_OBJETIVOS,
  OBJETIVOS,
  PERFILES_LABORALES,
  PREGUNTAS_SEGURIDAD,
  SEXOS_PERFIL,
  ZONAS_CORPORALES,
  type BloqueCondiciones,
  type BloqueObjetivos,
  type BloqueSeguridad,
  type CuestionarioSaludDto,
  type EvaluacionSalud,
  type GuardarPerfilRequest,
  type NivelPrecaucion,
  type PerfilDto,
  type RestriccionSalud,
} from '@/features/perfil/types'
import { LIMITES_CORPORALES } from '@/features/publico/types'
import { db, type CuestionarioMock, type PerfilMock } from '../db'
import { autenticar, latencia, leerJson, problema, respuestas } from '../http'
import { sinConsentimientoSalud } from './progreso'

// Réplica de ProfileController (HabitTracker v2, Fase 2). El nivel 1 del perfil (jornada, zona
// horaria y aviso) se guarda sin consentimiento; peso, altura y el cuestionario exigen datosSalud
// (409 consentimiento_salud_requerido). Las reglas de la evaluación son las de EvaluadorSalud.

const ROLES = ['Estandar' as const]

const enRango = (v: unknown, { min, max }: { min: number; max: number }, entero = false) =>
  typeof v === 'number' &&
  Number.isFinite(v) &&
  v >= min &&
  v <= max &&
  (!entero || Number.isInteger(v))
const esUno = <T extends string>(lista: readonly T[], v: unknown): v is T =>
  typeof v === 'string' && (lista as readonly string[]).includes(v)
const nuloO = (v: unknown, valido: (v: unknown) => boolean) =>
  v === null || v === undefined || valido(v)
const esHora = (v: unknown) =>
  typeof v === 'string' && /^([01]\d|2[0-3]):[0-5]\d(:[0-5]\d)?$/.test(v)
// Igual que TimeZoneInfo en el backend: Intl solo acepta zonas IANA que conoce.
function esZona(v: unknown): boolean {
  if (typeof v !== 'string' || !v.trim() || v.length > 64) return false
  try {
    new Intl.DateTimeFormat('es', { timeZone: v.trim() })
    return true
  } catch {
    return false
  }
}

function perfilValido(b: Partial<GuardarPerfilRequest> | null): b is GuardarPerfilRequest {
  return (
    !!b &&
    esUno(PERFILES_LABORALES, b.perfilLaboral) &&
    esZona(b.zonaHoraria) &&
    b.avisoSaludAceptado === true &&
    [b.horaEntradaTrabajo, b.horaSalidaTrabajo, b.horaDespertar, b.horaDormir].every((h) =>
      nuloO(h, esHora),
    ) &&
    nuloO(b.edad, (v) => enRango(v, LIMITES_CORPORALES.edad, true)) &&
    nuloO(b.sexo, (v) => esUno(SEXOS_PERFIL, v)) &&
    nuloO(b.alturaCm, (v) => enRango(v, LIMITES_CORPORALES.alturaCm)) &&
    nuloO(b.pesoKg, (v) => enRango(v, LIMITES_CORPORALES.pesoKg)) &&
    nuloO(b.actividad, (v) => esUno(ACTIVIDADES_PERFIL, v)) &&
    nuloO(b.diasPorSemana, (v) => enRango(v, LIMITES_PLAN.diasPorSemana, true)) &&
    nuloO(b.minutosPorSesion, (v) => enRango(v, LIMITES_PLAN.minutosPorSesion, true)) &&
    nuloO(
      b.metaSlug,
      (v) => typeof v === 'string' && /^[a-z0-9]+(-[a-z0-9]+)*$/.test(v) && v.length <= 60,
    )
  )
}

const aDto = (nombre: string, p: PerfilMock): PerfilDto => ({ nombre, ...p })

// --- Réplica de EvaluadorSalud ---

// Orden de declaración de RestriccionSalud en el backend.
const ORDEN_RESTRICCIONES: RestriccionSalud[] = [
  'SoloIntensidadBaja',
  'SinIntensidadAlta',
  'SinImpacto',
  'SinRestriccionCalorica',
  'EvitarZonaRodilla',
  'EvitarZonaEspaldaBaja',
  'EvitarZonaHombro',
]

function imc(p: PerfilMock | undefined): number | null {
  if (!p?.pesoKg || !p.alturaCm) return null
  return Math.round((p.pesoKg / (p.alturaCm / 100) ** 2) * 10) / 10
}

export function evaluar(
  c: CuestionarioMock | undefined,
  p: PerfilMock | undefined,
  usarSalud: boolean,
): EvaluacionSalud {
  const cuestionario = usarSalud ? c : undefined
  const r = new Set<RestriccionSalud>()
  let nivel: NivelPrecaucion = 'Normal'
  const precaucion = () => {
    if (nivel === 'Normal') nivel = 'Precaucion'
  }
  if (cuestionario?.seguridad && Object.values(cuestionario.seguridad).some(Boolean)) {
    nivel = 'ConsultarMedico'
    r.add('SoloIntensidadBaja').add('SinIntensidadAlta')
  }
  for (const cond of cuestionario?.condiciones?.condiciones ?? []) {
    if (
      ['Hipertension', 'EnfermedadCardiaca', 'EnfermedadRenal', 'Diabetes', 'Asma'].includes(cond)
    )
      r.add('SinIntensidadAlta')
    if (cond === 'Embarazo' || cond === 'Posparto')
      r.add('SinIntensidadAlta').add('SinRestriccionCalorica')
    if (cond === 'HistorialTrastornoAlimentario') r.add('SinRestriccionCalorica')
    if (cond === 'Rodilla') r.add('SinImpacto').add('EvitarZonaRodilla')
    if (cond === 'EspaldaBaja') r.add('EvitarZonaEspaldaBaja')
    if (cond === 'Hombro') r.add('EvitarZonaHombro')
    if (cond === 'Osteoporosis') r.add('SinImpacto')
    if (cond !== 'Ninguna') precaucion()
  }
  if (p?.edad != null && p.edad < 18) r.add('SinRestriccionCalorica').add('SinIntensidadAlta')
  const indice = usarSalud ? imc(p) : null
  if (indice !== null && indice < 18.5) {
    r.add('SinRestriccionCalorica')
    precaucion()
  }
  return {
    nivel,
    // Mismo orden que el backend (SortedSet / OrderBy sobre el valor del enum), no alfabético.
    restricciones: [...r].sort(
      (a, b) => ORDEN_RESTRICCIONES.indexOf(a) - ORDEN_RESTRICCIONES.indexOf(b),
    ),
    alergenosAEvitar: (cuestionario?.condiciones?.alergias ?? [])
      .filter((a) => a !== 'Ninguna')
      .sort((a, b) => ALERGIAS.indexOf(a) - ALERGIAS.indexOf(b)),
    completitud: {
      objetivos: !!cuestionario?.objetivos,
      seguridad: !!cuestionario?.seguridad,
      condiciones: !!cuestionario?.condiciones,
    },
  }
}

function cuestionarioDto(usuarioId: string): CuestionarioSaludDto {
  const c = db.cuestionarios.get(usuarioId)
  const usa = !!db.consentimientos.get(usuarioId)?.datosSalud
  return {
    version: 1,
    objetivos: c?.objetivos ?? null,
    objetivosRespondidoEn: c?.objetivosRespondidoEn ?? null,
    seguridad: c?.seguridad ?? null,
    seguridadRespondidoEn: c?.seguridadRespondidoEn ?? null,
    condiciones: c?.condiciones ?? null,
    condicionesRespondidoEn: c?.condicionesRespondidoEn ?? null,
    evaluacion: evaluar(c, db.perfiles.get(usuarioId), usa),
    usaDatosDeSalud: usa,
  }
}

// Sección "perfilYSalud" de la descarga de datos (DatosPerfilSaludDto): perfil, cuestionario, pesos y medidas.
export function datosPerfilSalud(usuarioId: string) {
  const perfil = db.perfiles.get(usuarioId)
  const cuestionario = db.cuestionarios.get(usuarioId)
  return {
    // PerfilDto.Desde deja el nombre vacío: en la descarga lo da la sección de la cuenta.
    perfil: perfil ? aDto('', perfil) : null,
    cuestionario: cuestionario ? { ...cuestionarioDto(usuarioId), usaDatosDeSalud: true } : null,
    pesos: db.pesos.get(usuarioId)?.registros ?? [],
    mediciones: db.mediciones.get(usuarioId) ?? [],
  }
}

const sinRepetir = (l: readonly string[]) => new Set(l).size === l.length
const listaConNinguna = (l: unknown, valores: readonly string[]) =>
  Array.isArray(l) &&
  l.length > 0 &&
  l.every((v) => esUno(valores, v)) &&
  sinRepetir(l) &&
  (!l.includes('Ninguna') || l.length === 1)

function objetivosValidos(b: Partial<BloqueObjetivos> | null): b is BloqueObjetivos {
  const objetivos = b?.objetivos
  const zonas = b?.zonas ?? []
  return (
    !!b &&
    Array.isArray(objetivos) &&
    objetivos.length >= 1 &&
    objetivos.length <= MAXIMO_OBJETIVOS &&
    objetivos.every((o) => esUno(OBJETIVOS, o)) &&
    sinRepetir(objetivos) &&
    Array.isArray(zonas) &&
    zonas.every((z) => esUno(ZONAS_CORPORALES, z)) &&
    sinRepetir(zonas) &&
    (zonas.length === 0 || objetivos.includes('DefinirTonificar')) &&
    esUno(EXPERIENCIAS, b.experiencia) &&
    esUno(LUGARES, b.lugar)
  )
}

const seguridadValida = (b: Partial<BloqueSeguridad> | null): b is BloqueSeguridad =>
  !!b && PREGUNTAS_SEGURIDAD.every((k) => typeof b[k] === 'boolean')

const condicionesValidas = (b: Partial<BloqueCondiciones> | null): b is BloqueCondiciones =>
  !!b && listaConNinguna(b.condiciones, CONDICIONES) && listaConNinguna(b.alergias, ALERGIAS)

function guardarBloque<K extends 'objetivos' | 'seguridad' | 'condiciones'>(
  usuarioId: string,
  clave: K,
  valor: NonNullable<CuestionarioMock[K]>,
) {
  const actual: CuestionarioMock = db.cuestionarios.get(usuarioId) ?? {
    objetivos: null,
    objetivosRespondidoEn: null,
    seguridad: null,
    seguridadRespondidoEn: null,
    condiciones: null,
    condicionesRespondidoEn: null,
  }
  db.cuestionarios.set(usuarioId, {
    ...actual,
    [clave]: valor,
    [`${clave}RespondidoEn`]: new Date().toISOString(),
  })
}

export const perfilHandlers = [
  http.get('/api/profile', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const perfil = db.perfiles.get(sesion.usuario.id)
    if (!perfil)
      return problema(
        404,
        'No encontrado.',
        'Aún no completaste tu perfil.',
        'perfil_no_encontrado',
      )
    return HttpResponse.json(aDto(sesion.usuario.nombre, perfil))
  }),

  http.put('/api/profile', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<GuardarPerfilRequest>(request)
    if (!perfilValido(body)) return respuestas.invalida()
    const anterior = db.perfiles.get(sesion.usuario.id)
    // Solo peso y altura son datos de salud, y solo pide el permiso un valor NUEVO (como PerfilService): quien lo
    // retiró puede seguir cambiando su jornada aunque el formulario reenvíe el peso guardado.
    const traeSalud = body.pesoKg != null || body.alturaCm != null
    const cambiaSalud =
      traeSalud &&
      (!anterior ||
        (body.pesoKg ?? null) !== anterior.pesoKg ||
        (body.alturaCm ?? null) !== anterior.alturaCm)
    if (cambiaSalud) {
      const rechazo = sinConsentimientoSalud(sesion.usuario.id)
      if (rechazo) return rechazo
    }
    const metaSlug = body.metaSlug ?? null
    // El plan de interés empieza al elegirlo y vuelve a empezar solo si cambia.
    const inicioPlan =
      metaSlug === null
        ? null
        : anterior?.metaSlug === metaSlug
          ? anterior.inicioPlan
          : new Date().toISOString()
    const perfil: PerfilMock = {
      perfilLaboral: body.perfilLaboral,
      zonaHoraria: body.zonaHoraria.trim(),
      avisoSaludAceptado: true,
      horaEntradaTrabajo: body.horaEntradaTrabajo ?? null,
      horaSalidaTrabajo: body.horaSalidaTrabajo ?? null,
      horaDespertar: body.horaDespertar ?? null,
      horaDormir: body.horaDormir ?? null,
      edad: body.edad ?? null,
      sexo: body.sexo ?? null,
      alturaCm: body.alturaCm ?? null,
      pesoKg: body.pesoKg ?? null,
      actividad: body.actividad ?? null,
      diasPorSemana: body.diasPorSemana ?? null,
      minutosPorSesion: body.minutosPorSesion ?? null,
      metaSlug,
      inicioPlan,
      actualizadoEn: new Date().toISOString(),
    }
    db.perfiles.set(sesion.usuario.id, perfil)
    return HttpResponse.json(aDto(sesion.usuario.nombre, perfil))
  }),

  http.get('/api/profile/health', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    return HttpResponse.json(cuestionarioDto(sesion.usuario.id))
  }),

  http.put('/api/profile/health/objetivos', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<BloqueObjetivos>(request)
    if (!objetivosValidos(body)) return respuestas.invalida()
    const rechazo = sinConsentimientoSalud(sesion.usuario.id)
    if (rechazo) return rechazo
    guardarBloque(sesion.usuario.id, 'objetivos', { ...body, zonas: body.zonas ?? [] })
    return HttpResponse.json(cuestionarioDto(sesion.usuario.id))
  }),

  http.put('/api/profile/health/seguridad', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<BloqueSeguridad>(request)
    if (!seguridadValida(body)) return respuestas.invalida()
    const rechazo = sinConsentimientoSalud(sesion.usuario.id)
    if (rechazo) return rechazo
    guardarBloque(sesion.usuario.id, 'seguridad', body)
    return HttpResponse.json(cuestionarioDto(sesion.usuario.id))
  }),

  http.put('/api/profile/health/condiciones', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<BloqueCondiciones>(request)
    if (!condicionesValidas(body)) return respuestas.invalida()
    const rechazo = sinConsentimientoSalud(sesion.usuario.id)
    if (rechazo) return rechazo
    guardarBloque(sesion.usuario.id, 'condiciones', body)
    return HttpResponse.json(cuestionarioDto(sesion.usuario.id))
  }),
]
