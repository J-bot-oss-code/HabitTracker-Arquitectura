import { http, HttpResponse } from 'msw'
import type { SolicitudBajaRequest, SolicitudBajaResponse } from '@/features/privacidad/types'
import { DIAS_DE_GRACIA, MODOS_BAJA } from '@/features/privacidad/types'
import { db } from '../../db'
import { auditar } from '../../auditoria'
import { autenticar, latencia, leerJson, problema, respuestas } from '../../http'
import type { ContratoPropuesto } from './index'

// Contrato propuesto para el backend (mejora futura): Centro de privacidad (docs/mejoras §7.1).
// Consultar consentimientos, registrar la descarga de datos (para la auditoría) y pedir la baja
// con confirmación de contraseña y periodo de gracia.
export const contratosPrivacidad: ContratoPropuesto[] = [
  {
    metodo: 'GET',
    ruta: '/api/privacy/consents',
    proposito: 'Consentimientos vigentes del usuario y las versiones que aceptó.',
    roles: ['Estandar'],
    origen: 'mejoras §7.1 (revocar consentimientos)',
  },
  {
    metodo: 'POST',
    ruta: '/api/privacy/export-request',
    proposito: 'Deja constancia (auditoría) de que el usuario descargó sus datos.',
    roles: ['Estandar'],
    origen: 'mejoras §7.1 (acceso y portabilidad)',
  },
  {
    metodo: 'POST',
    ruta: '/api/privacy/deletion-request',
    proposito:
      'Desactiva o pide eliminar la cuenta, confirmando con la contraseña. El servidor debe aplicar el mismo bloqueo por intentos fallidos que el inicio de sesión (RF-CA) y revocar las sesiones al aceptar.',
    roles: ['Estandar'],
    origen: 'mejoras §7.1 (eliminación de cuenta)',
  },
]

function bajaValida(b: Partial<SolicitudBajaRequest> | null): b is SolicitudBajaRequest {
  return (
    !!b &&
    MODOS_BAJA.some((m) => m === b.modo) &&
    typeof b.password === 'string' &&
    b.password.length > 0 &&
    b.password.length <= 1024
  )
}

export const privacidadHandlers = [
  http.get('/api/privacy/consents', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const consentimientos = db.consentimientos.get(sesion.usuario.id)
    if (!consentimientos) return respuestas.noEncontrado('No hay consentimientos registrados.')
    return HttpResponse.json(consentimientos)
  }),

  http.post('/api/privacy/export-request', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    auditar({ actor: sesion.usuario.correo, accion: 'DescargaDatos', entidad: 'Cuenta' })
    return new HttpResponse(null, { status: 204 })
  }),

  http.post('/api/privacy/deletion-request', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Estandar'])
    if (error) return error
    const body = await leerJson<SolicitudBajaRequest>(request)
    if (!bajaValida(body)) return respuestas.invalida()
    // Un 401 cerraría la sesión en el cliente: la contraseña errónea es un 400 con motivo.
    if (sesion.usuario.password === null || body.password !== sesion.usuario.password) {
      return problema(400, 'Solicitud inválida.', 'La contraseña no es correcta.')
    }

    const ahora = Date.now()
    const borradoEn =
      body.modo === 'eliminar' ? new Date(ahora + DIAS_DE_GRACIA * 86_400_000).toISOString() : null
    db.solicitudesBaja.push({
      usuarioId: sesion.usuario.id,
      tipo: body.modo,
      solicitadaEn: new Date(ahora).toISOString(),
      borradoEn,
    })
    auditar({
      actor: sesion.usuario.correo,
      accion: 'SolicitudBaja',
      entidad: 'Cuenta',
      entidadId: sesion.usuario.id,
      valorNuevo: body.modo,
    })
    // La cuenta deja de iniciar sesión y se revocan las sesiones abiertas.
    sesion.usuario.activo = false
    sesion.usuario.sesionesValidasDesde = ahora
    const respuesta: SolicitudBajaResponse = { modo: body.modo, borradoEn }
    return HttpResponse.json(respuesta, { status: 202 })
  }),
]
