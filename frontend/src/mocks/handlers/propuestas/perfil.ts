import { http, HttpResponse } from 'msw'
import type {
  ConsentimientosDto,
  GuardarConsentimientosRequest,
  GuardarPerfilRequest,
  PerfilDto,
} from '@/features/perfil/types'
import { LIMITES_PLAN } from '@/features/perfil/schemas'
import { buscarMeta, VERSION_PRIVACIDAD, VERSION_TERMINOS } from '@/features/publico/api'
import { LIMITES_CORPORALES, NIVELES_ACTIVIDAD, SEXOS } from '@/features/publico/types'
import { db, type PerfilMock } from '../../db'
import { autenticar, latencia, leerJson, problema, respuestas } from '../../http'
import type { ContratoPropuesto } from './index'

// Contrato propuesto para el backend (mejora futura): perfil físico del onboarding y
// consentimientos versionados (docs/mejoras §3 y §7). El "servidor" vuelve a validar todo
// (RD-07) y no guarda datos de salud sin el consentimiento correspondiente.
export const contratosPerfil: ContratoPropuesto[] = [
  {
    metodo: 'GET',
    ruta: '/api/profile',
    proposito: 'Perfil físico y meta del usuario; 404 si aún no completó el onboarding.',
    roles: ['Estandar'],
    origen: 'mejoras §3 (onboarding)',
  },
  {
    metodo: 'PUT',
    ruta: '/api/profile',
    proposito: 'Crea o reemplaza el perfil. Exige el consentimiento de datos de salud.',
    roles: ['Estandar'],
    origen: 'mejoras §3 (onboarding) y §7.1 (rectificación)',
  },
  {
    metodo: 'PUT',
    ruta: '/api/privacy/consents',
    proposito: 'Registra las versiones aceptadas y los consentimientos opcionales.',
    roles: ['Estandar'],
    origen: 'mejoras §3 (ConsentRecord) y §7.1',
  },
]

const enRango = (v: unknown, { min, max }: { min: number; max: number }, entero = false) =>
  typeof v === 'number' &&
  Number.isFinite(v) &&
  v >= min &&
  v <= max &&
  (!entero || Number.isInteger(v))

const esUno = <T extends string>(lista: readonly T[], v: unknown): v is T =>
  typeof v === 'string' && (lista as readonly string[]).includes(v)

function perfilValido(b: Partial<GuardarPerfilRequest> | null): b is GuardarPerfilRequest {
  return (
    !!b &&
    enRango(b.edad, LIMITES_CORPORALES.edad, true) &&
    (b.sexo === null || esUno(SEXOS, b.sexo)) &&
    enRango(b.alturaCm, LIMITES_CORPORALES.alturaCm) &&
    enRango(b.pesoKg, LIMITES_CORPORALES.pesoKg) &&
    esUno(NIVELES_ACTIVIDAD, b.actividad) &&
    enRango(b.diasPorSemana, LIMITES_PLAN.diasPorSemana, true) &&
    enRango(b.minutosPorSesion, LIMITES_PLAN.minutosPorSesion, true) &&
    (b.metaSlug === null || buscarMeta(b.metaSlug) !== null) &&
    b.avisoSaludAceptado === true
  )
}

function consentimientosValidos(
  b: Partial<GuardarConsentimientosRequest> | null,
  aceptadas?: ConsentimientosDto,
): b is GuardarConsentimientosRequest {
  // Se acepta la versión vigente de cada documento o la que el usuario ya había aceptado:
  // cambiar un interruptor no debe obligar a aceptar documentos nuevos que no ha visto.
  const versionOk = (enviada: unknown, vigente: string, previa: string | null | undefined) =>
    enviada === vigente || (previa != null && enviada === previa)
  return (
    !!b &&
    versionOk(b.terminosVersion, VERSION_TERMINOS, aceptadas?.terminosVersion) &&
    versionOk(b.privacidadVersion, VERSION_PRIVACIDAD, aceptadas?.privacidadVersion) &&
    typeof b.datosSalud === 'boolean' &&
    typeof b.marketing === 'boolean' &&
    typeof b.compartirCumplimiento === 'boolean' &&
    typeof b.estadisticasAnonimas === 'boolean'
  )
}

const aDto = (nombre: string, p: PerfilMock): PerfilDto => ({ nombre, ...p })

export const perfilHandlers = [
  http.get('/api/profile', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const perfil = db.perfiles.get(sesion.usuario.id)
    if (!perfil) return respuestas.noEncontrado('El perfil no existe.')
    return HttpResponse.json(aDto(sesion.usuario.nombre, perfil))
  }),

  http.put('/api/profile', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const body = await leerJson<GuardarPerfilRequest>(request)
    if (!perfilValido(body)) return respuestas.invalida()
    // Peso, medidas y actividad son datos de salud: sin consentimiento no se guardan.
    if (!db.consentimientos.get(sesion.usuario.id)?.datosSalud) {
      return problema(
        409,
        'Falta el consentimiento.',
        'Acepta el tratamiento de datos de salud para guardar tu perfil.',
      )
    }
    const anterior = db.perfiles.get(sesion.usuario.id)
    // El plan empieza al elegir una meta y vuelve a empezar si se cambia por otra.
    const inicioPlan =
      body.metaSlug === null
        ? null
        : anterior?.metaSlug === body.metaSlug
          ? anterior.inicioPlan
          : new Date().toISOString()
    const perfil: PerfilMock = {
      edad: body.edad,
      sexo: body.sexo,
      alturaCm: body.alturaCm,
      pesoKg: body.pesoKg,
      actividad: body.actividad,
      diasPorSemana: body.diasPorSemana,
      minutosPorSesion: body.minutosPorSesion,
      metaSlug: body.metaSlug,
      inicioPlan,
      avisoSaludAceptado: true,
    }
    db.perfiles.set(sesion.usuario.id, perfil)
    return HttpResponse.json(aDto(sesion.usuario.nombre, perfil))
  }),

  http.put('/api/privacy/consents', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const body = await leerJson<GuardarConsentimientosRequest>(request)
    const anterior = db.consentimientos.get(sesion.usuario.id)
    if (!consentimientosValidos(body, anterior)) return respuestas.invalida()
    const mismasVersiones =
      anterior?.terminosVersion === body.terminosVersion &&
      anterior?.privacidadVersion === body.privacidadVersion
    const consentimientos: ConsentimientosDto = {
      terminosVersion: body.terminosVersion,
      privacidadVersion: body.privacidadVersion,
      // La fecha de aceptación la fija el servidor y solo cambia con una versión nueva.
      aceptadoEn:
        mismasVersiones && anterior?.aceptadoEn ? anterior.aceptadoEn : new Date().toISOString(),
      datosSalud: body.datosSalud,
      marketing: body.marketing,
      compartirCumplimiento: body.compartirCumplimiento,
      estadisticasAnonimas: body.estadisticasAnonimas,
    }
    db.consentimientos.set(sesion.usuario.id, consentimientos)
    return HttpResponse.json(consentimientos)
  }),
]
