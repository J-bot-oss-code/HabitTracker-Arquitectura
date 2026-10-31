import { http, HttpResponse } from 'msw'
import type { SolicitudPermisoDto } from '@/features/permissions/types'
import { db } from '../db'
import { autenticar, esTexto, latencia, leerJson, respuestas } from '../http'

// Réplica de Permissions.Domain: Pendiente → Aprobada → Aplicada, Pendiente → Rechazada.
export function tienePermisoAplicado(correo: string, permiso: string): boolean {
  return db.solicitudes.some(
    (s) =>
      s.solicitante.toLowerCase() === correo.toLowerCase() &&
      s.permisoSolicitado === permiso &&
      s.estado === 'Aplicada',
  )
}

const esPropia = (s: SolicitudPermisoDto, correo: string) =>
  s.solicitante.toLowerCase() === correo.toLowerCase()

export const permissionsHandlers = [
  http.post('/api/permissions', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const body = await leerJson<{ permisoSolicitado: string }>(request)
    if (!body || !esTexto(body.permisoSolicitado, 1, 256)) return respuestas.invalida()

    const solicitud: SolicitudPermisoDto = {
      id: crypto.randomUUID(),
      solicitante: sesion.usuario.correo,
      permisoSolicitado: body.permisoSolicitado.trim(),
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
    const solicitud = db.solicitudes.find((s) => s.id === params.id)
    if (!solicitud) return respuestas.noEncontrado('La solicitud no existe.')
    if (sesion.usuario.rol !== 'Administrador' && !esPropia(solicitud, sesion.usuario.correo)) {
      return respuestas.prohibido()
    }
    return HttpResponse.json(solicitud)
  }),

  http.post<{ id: string }>('/api/permissions/:id/approve', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ['Administrador'])
    if (error) return error
    const solicitud = db.solicitudes.find((s) => s.id === params.id)
    if (!solicitud) return respuestas.noEncontrado('La solicitud no existe.')
    // RF-GP-07: nadie resuelve su propia solicitud.
    if (solicitud.estado !== 'Pendiente' || esPropia(solicitud, sesion.usuario.correo)) {
      return respuestas.operacion()
    }
    // Aprobar y aplicar ocurren juntos en SolicitudPermisoService.
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
    const solicitud = db.solicitudes.find((s) => s.id === params.id)
    if (!solicitud) return respuestas.noEncontrado('La solicitud no existe.')
    if (solicitud.estado !== 'Pendiente' || esPropia(solicitud, sesion.usuario.correo)) {
      return respuestas.operacion()
    }
    Object.assign(solicitud, {
      estado: 'Rechazada',
      aprobador: sesion.usuario.correo,
      fechaResolucion: new Date().toISOString(),
      motivoRechazo: body.motivoRechazo.trim(),
    })
    return respuestas.mensaje('Solicitud rechazada.')
  }),
]
