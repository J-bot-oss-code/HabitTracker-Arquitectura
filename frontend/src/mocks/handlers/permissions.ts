import { auditar } from '../auditoria'
import { http, HttpResponse } from 'msw'
import type { SolicitudPermisoDto } from '@/features/permissions/types'
import { db } from '../db'
import { CLAVE_PERMISO } from '@/features/permissions/schemas'
import { autenticar, esGuid, esTexto, latencia, leerJson, problema, respuestas } from '../http'

// Réplica de Permissions.Domain: Pendiente → Aprobada → Aplicada, Pendiente → Rechazada.
export function tienePermisoAplicado(correo: string, permiso: string): boolean {
  return db.solicitudes.some(
    (s) =>
      s.solicitante.toLowerCase() === correo.toLowerCase() &&
      s.permisoSolicitado.toLowerCase() === permiso.trim().toLowerCase() &&
      s.estado === 'Aplicada',
  )
}

// ExisteVigenteAsync: una solicitud Pendiente o Aplicada del mismo permiso de esa persona (sin mayúsculas).
// Una Rechazada no cuenta: se puede volver a pedir.
const existeVigente = (correo: string, permiso: string) =>
  db.solicitudes.some(
    (s) =>
      s.solicitante.toLowerCase() === correo.toLowerCase() &&
      s.permisoSolicitado.toLowerCase() === permiso.toLowerCase() &&
      (s.estado === 'Pendiente' || s.estado === 'Aplicada'),
  )

// El 404 de una solicitud que no existe lo responde el middleware (SolicitudNoEncontradaException), con cuerpo y código.
const solicitudNoEncontrada = () =>
  problema(404, 'No encontrado.', 'La solicitud no existe.', 'solicitud_no_encontrada')

// ValidationProblemDetails de [ApiController]: la clave debe ser un nombre con puntos (CrearPermisoRequest).
const claveInvalida = () =>
  HttpResponse.json(
    {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { PermisoSolicitado: ['El permiso no es válido.'] },
    },
    { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
  )

const esClaveValida = (v: unknown): v is string =>
  typeof v === 'string' && v.length >= 3 && v.length <= 256 && CLAVE_PERMISO.test(v)

const esPropia = (s: SolicitudPermisoDto, correo: string) =>
  s.solicitante.toLowerCase() === correo.toLowerCase()

export const permissionsHandlers = [
  http.post('/api/permissions', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const body = await leerJson<{ permisoSolicitado: string }>(request)
    if (!body) return respuestas.invalida()
    if (!esClaveValida(body.permisoSolicitado)) return claveInvalida()
    if (existeVigente(sesion.usuario.correo, body.permisoSolicitado)) {
      return problema(
        409,
        'Solicitud duplicada.',
        'Ya tienes una solicitud vigente de este permiso.',
        'solicitud_duplicada',
      )
    }

    const solicitud: SolicitudPermisoDto = {
      id: crypto.randomUUID(),
      solicitante: sesion.usuario.correo,
      permisoSolicitado: body.permisoSolicitado,
      estado: 'Pendiente',
      aprobador: null,
      fechaCreacion: new Date().toISOString(),
      fechaResolucion: null,
      motivoRechazo: null,
    }
    db.solicitudes.push(solicitud)
    return HttpResponse.json({ id: solicitud.id }, { status: 201 })
  }),

  http.get('/api/permissions', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    // Igual que el controlador: soloPendientes vale true si no se envía.
    const soloPendientes = new URL(request.url).searchParams.get('soloPendientes') !== 'false'
    const esAdmin = sesion.usuario.rol === 'Administrador'
    const visibles = db.solicitudes.filter(
      (s) =>
        (esAdmin || esPropia(s, sesion.usuario.correo)) &&
        (!soloPendientes || s.estado === 'Pendiente'),
    )
    return HttpResponse.json(visibles)
  }),

  http.get<{ permiso: string }>(
    '/api/permissions/effective/:permiso',
    async ({ request, params }) => {
      await latencia()
      const { sesion, error } = autenticar(request)
      if (error) return error
      if (!esTexto(params.permiso, 1, 256)) return respuestas.invalida()
      return tienePermisoAplicado(sesion.usuario.correo, params.permiso)
        ? HttpResponse.json({ permiso: params.permiso, efectivo: true })
        : respuestas.prohibido()
    },
  ),

  http.get<{ permiso: string }>(
    '/api/permissions/actions/:permiso/execute',
    async ({ request, params }) => {
      await latencia()
      const { sesion, error } = autenticar(request)
      if (error) return error
      if (!tienePermisoAplicado(sesion.usuario.correo, params.permiso))
        return respuestas.prohibido()
      return HttpResponse.json(db.solicitudes.filter((s) => esPropia(s, sesion.usuario.correo)))
    },
  ),

  http.get<{ id: string }>('/api/permissions/:id', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    if (!esGuid(params.id)) return new HttpResponse(null, { status: 404 })
    const solicitud = db.solicitudes.find((s) => s.id === params.id)
    if (!solicitud) return solicitudNoEncontrada()
    if (sesion.usuario.rol !== 'Administrador' && !esPropia(solicitud, sesion.usuario.correo)) {
      return respuestas.prohibido()
    }
    return HttpResponse.json(solicitud)
  }),

  http.post<{ id: string }>('/api/permissions/:id/approve', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Administrador'])
    if (error) return error
    if (!esGuid(params.id)) return new HttpResponse(null, { status: 404 })
    const solicitud = db.solicitudes.find((s) => s.id === params.id)
    if (!solicitud) return solicitudNoEncontrada()
    // RF-GP-07: nadie resuelve su propia solicitud.
    if (solicitud.estado !== 'Pendiente' || esPropia(solicitud, sesion.usuario.correo)) {
      return respuestas.operacion()
    }
    // Aprobar y aplicar ocurren juntos en SolicitudPermisoService.
    auditar({
      actor: sesion.usuario.correo,
      accion: 'ResolucionPermiso',
      entidad: 'SolicitudPermiso',
      entidadId: solicitud.id,
      valorAnterior: 'Pendiente',
      valorNuevo: 'Aplicada',
    })
    Object.assign(solicitud, {
      estado: 'Aplicada',
      aprobador: sesion.usuario.correo,
      fechaResolucion: new Date().toISOString(),
    })
    return respuestas.mensaje('Solicitud aprobada y aplicada.')
  }),

  http.post<{ id: string }>('/api/permissions/:id/reject', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Administrador'])
    if (error) return error
    const body = await leerJson<{ motivoRechazo: string }>(request)
    if (!body || !esTexto(body.motivoRechazo, 1, 500)) return respuestas.invalida()
    if (!esGuid(params.id)) return new HttpResponse(null, { status: 404 })
    const solicitud = db.solicitudes.find((s) => s.id === params.id)
    if (!solicitud) return solicitudNoEncontrada()
    if (solicitud.estado !== 'Pendiente' || esPropia(solicitud, sesion.usuario.correo)) {
      return respuestas.operacion()
    }
    auditar({
      actor: sesion.usuario.correo,
      accion: 'ResolucionPermiso',
      entidad: 'SolicitudPermiso',
      entidadId: solicitud.id,
      valorAnterior: 'Pendiente',
      valorNuevo: 'Rechazada',
    })
    Object.assign(solicitud, {
      estado: 'Rechazada',
      aprobador: sesion.usuario.correo,
      fechaResolucion: new Date().toISOString(),
      motivoRechazo: body.motivoRechazo.trim(),
    })
    return respuestas.mensaje('Solicitud rechazada.')
  }),
]
