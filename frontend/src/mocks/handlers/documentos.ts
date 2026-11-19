import { http, HttpResponse } from 'msw'
import {
  EXTENSIONES_PERMITIDAS,
  LONGITUD_MAXIMA_NOMBRE,
  MAXIMO_ADJUNTOS_POR_PLAN,
  TAMANO_MAXIMO_BYTES,
  claveEliminarArchivo,
  type AdjuntoPlanDto,
  type DocumentoDto,
  type VinculoDocumentoDto,
} from '@/features/documentos/types'
import { db, type DocumentoMock, type PlanUsuarioMock, type UsuarioMock } from '../db'
import { autenticar, esGuid, latencia, leerJson, problema, respuestas } from '../http'
import { tienePermisoAplicado } from './permissions'

// Réplica de DocumentsController y DocumentoService (Manejador de documentos) y de AdjuntosPlanController y
// AdjuntosPlanService (HabitTracker): la identidad sale del token, un archivo eliminado cuenta como inexistente y
// uno ajeno responde 403 (documentos) o 404 (adjuntos). El borrado es lógico.

// Lista blanca extensión → tipo: el tipo que se guarda sale de aquí, nunca del cliente.
const TIPOS: Record<(typeof EXTENSIONES_PERMITIDAS)[number], string> = {
  '.pdf': 'application/pdf',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.txt': 'text/plain',
  '.docx': 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
  '.xlsx': 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
}

// [RequestSizeLimit] del controller: un poco más que el máximo del archivo, para los bytes del multipart.
const LIMITE_CUERPO_BYTES = 11 * 1024 * 1024

const ROLES = ['Estandar' as const]

const enCurso = (plan: PlanUsuarioMock) => plan.estado === 'Activo' || plan.estado === 'Pausado'
const mismoId = (a: string, b: string) => a.toLowerCase() === b.toLowerCase()

const archivoInvalido = (detalle: string) => problema(400, 'Archivo no válido.', detalle)
const documentoNoEncontrado = () => problema(404, 'No encontrado.', 'El documento no existe.')
const sinCuerpo404 = () => new HttpResponse(null, { status: 404 })
const requierePermiso = (detalle: string) =>
  problema(409, 'Se necesita un permiso.', detalle, 'documento_requiere_permiso')

const aDto = (d: DocumentoMock): DocumentoDto => ({
  id: d.id,
  nombre: d.nombre,
  tipo: d.tipo,
  tamanoBytes: d.tamanoBytes,
  fechaSubidaUtc: d.fechaSubidaUtc,
  usuarioId: d.usuarioId,
  correoUsuario: d.correoUsuario,
})

// DocumentoService.LimpiarNombre: el último segmento ("\" cuenta como separador) y sin caracteres de control.
function limpiarNombre(nombre: string): string | null {
  const limpio = nombre.trim().replaceAll('\\', '/').split('/').pop()!
  // eslint-disable-next-line no-control-regex
  return !limpio.trim() || /[\u0000-\u001f\u007f-\u009f]/.test(limpio) ? null : limpio
}

// DocumentoService.ObtenerAutorizadoAsync: eliminado = 404; ni dueño ni Administrador = 403 con cuerpo.
function documentoAutorizado(
  id: string,
  usuario: UsuarioMock,
): { documento: DocumentoMock; error?: never } | { documento?: never; error: Response } {
  if (!esGuid(id)) return { error: sinCuerpo404() }
  const documento = db.documentos.find((d) => mismoId(d.id, id))
  if (!documento || documento.eliminado) return { error: documentoNoEncontrado() }
  if (documento.usuarioId !== usuario.id && usuario.rol !== 'Administrador')
    return { error: respuestas.ajeno() }
  return { documento }
}

// IDocumentosUsuario.ObtenerAsync: solo los archivos propios y sin eliminar.
const documentoPropio = (id: string, usuarioId: string) =>
  db.documentos.find((d) => mismoId(d.id, id) && d.usuarioId === usuarioId && !d.eliminado)

// Plan propio; uno ajeno responde igual que uno que no existe.
function planPropio(id: string, usuarioId: string): PlanUsuarioMock | Response {
  if (!esGuid(id)) return sinCuerpo404()
  const plan = db.planesUsuario.find((p) => mismoId(p.id, id) && p.usuarioId === usuarioId)
  return (
    plan ??
    problema(404, 'No encontrado.', 'No encontramos este plan.', 'plan_usuario_no_encontrado')
  )
}

// GuardaEliminacionDocumentoAdapter: un archivo en un plan en curso solo lo elimina su dueño con el permiso
// Documents.DeleteLinked.{id} aplicado.
const estaEnPlanEnCurso = (documentoId: string, usuarioId: string) =>
  db.adjuntos.some(
    (a) =>
      mismoId(a.documentoId, documentoId) &&
      a.usuarioId === usuarioId &&
      db.planesUsuario.some((p) => p.id === a.planUsuarioId && enCurso(p)),
  )

const tienePermisoDeArchivo = (usuario: UsuarioMock, documentoId: string) =>
  tienePermisoAplicado(usuario.correo, claveEliminarArchivo(documentoId))

type ArchivoSubido = Blob & { name?: string }

// Un campo que no es texto es un archivo. No se usa `instanceof File`: el File que entrega fetch en las pruebas no es el de jsdom.
const archivoDeFormulario = (valor: FormDataEntryValue | null): ArchivoSubido | null =>
  valor !== null && typeof valor !== 'string' ? valor : null

export const documentosHandlers = [
  // --- Manejador de documentos (cualquier rol autenticado) ---
  http.post('/api/documents', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error

    // Un cuerpo que no es multipart equivale a «no llegó archivo» (IFormFile? queda en null).
    const archivo = await request
      .formData()
      .then((formulario) => archivoDeFormulario(formulario.get('archivo')))
      .catch(() => null)
    if (archivo && archivo.size > LIMITE_CUERPO_BYTES)
      return problema(413, 'Solicitud rechazada.', 'La solicitud supera el tamaño permitido.')

    // RD-07: el mismo orden de DocumentoService.SubirAsync.
    if (!archivo) return archivoInvalido('Debe adjuntar un archivo que no esté vacío.')
    const nombre = limpiarNombre(archivo.name ?? '')
    const extension = nombre ? nombre.slice(nombre.lastIndexOf('.')).toLowerCase() : ''
    if (!nombre || !Object.hasOwn(TIPOS, extension))
      return archivoInvalido('El tipo de archivo no está permitido.')
    if (archivo.size > TAMANO_MAXIMO_BYTES)
      return archivoInvalido('El archivo supera el tamaño máximo permitido de 10 MB.')
    if (archivo.size === 0) return archivoInvalido('Debe adjuntar un archivo que no esté vacío.')
    if (nombre.trim().length > LONGITUD_MAXIMA_NOMBRE)
      return archivoInvalido('Los datos del archivo no son válidos.')

    const documento: DocumentoMock = {
      id: crypto.randomUUID(),
      nombre: nombre.trim(),
      tipo: TIPOS[extension as keyof typeof TIPOS],
      tamanoBytes: archivo.size,
      fechaSubidaUtc: new Date().toISOString(),
      usuarioId: sesion.usuario.id,
      correoUsuario: sesion.usuario.correo,
      eliminado: false,
      contenido: await archivo.arrayBuffer(),
    }
    db.documentos.push(documento)
    return HttpResponse.json(aDto(documento), {
      status: 201,
      headers: { Location: `/api/documents/${documento.id}` },
    })
  }),

  // RF-DOC-02: el Administrador ve todos los no eliminados; el Estándar solo los suyos. Del más reciente al más antiguo.
  http.get('/api/documents', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const esAdmin = sesion.usuario.rol === 'Administrador'
    return HttpResponse.json(
      db.documentos
        .filter((d) => !d.eliminado && (esAdmin || d.usuarioId === sesion.usuario.id))
        .sort((a, b) => b.fechaSubidaUtc.localeCompare(a.fechaSubidaUtc))
        .map(aDto),
    )
  }),

  http.get<{ id: string }>('/api/documents/:id/download', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const { documento, error: rechazo } = documentoAutorizado(params.id, sesion.usuario)
    if (rechazo) return rechazo
    // Los documentos son privados: nosniff y sin caché. Content-Disposition lleva el nombre (RFC 5987).
    const simple = documento.nombre.replace(/[^\x20-\x7e]|["\\;]/g, '_')
    return new HttpResponse(documento.contenido, {
      headers: {
        'Content-Type': documento.tipo,
        'Content-Disposition': `attachment; filename="${simple}"; filename*=UTF-8''${encodeURIComponent(documento.nombre)}`,
        'X-Content-Type-Options': 'nosniff',
        'Cache-Control': 'no-store',
      },
    })
  }),

  http.get<{ id: string }>('/api/documents/:id', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const { documento, error: rechazo } = documentoAutorizado(params.id, sesion.usuario)
    return rechazo ?? HttpResponse.json(aDto(documento))
  }),

  http.delete<{ id: string }>('/api/documents/:id', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const { documento, error: rechazo } = documentoAutorizado(params.id, sesion.usuario)
    if (rechazo) return rechazo
    // Solo el dueño pasa por la guarda; el Administrador elimina como siempre.
    if (
      sesion.usuario.rol !== 'Administrador' &&
      estaEnPlanEnCurso(documento.id, sesion.usuario.id) &&
      !tienePermisoDeArchivo(sesion.usuario, documento.id)
    )
      return requierePermiso('Este archivo está en uso: pide permiso para eliminarlo.')
    // RF-DOC-04: solo se marca; el registro y los bytes permanecen.
    documento.eliminado = true
    return new HttpResponse(null, { status: 204 })
  }),

  // --- Archivos en los planes (solo el rol Estándar). Van antes de /api/my-plans/:id en handlers/index.ts. ---
  http.get('/api/my-plans/attachments', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    // Une el vínculo con el plan, del más antiguo al más nuevo. No mira si el archivo se eliminó: igual que el backend.
    const vinculos: VinculoDocumentoDto[] = db.adjuntos
      .filter((a) => a.usuarioId === sesion.usuario.id)
      .sort((a, b) => a.adjuntadoEnUtc.localeCompare(b.adjuntadoEnUtc))
      .flatMap((a) => {
        const plan = db.planesUsuario.find(
          (p) => p.id === a.planUsuarioId && p.usuarioId === sesion.usuario.id,
        )
        return plan
          ? [
              {
                documentoId: a.documentoId,
                planId: plan.id,
                planNombre: plan.nombre,
                enCurso: enCurso(plan),
              },
            ]
          : []
      })
    return HttpResponse.json(vinculos)
  }),

  http.get<{ id: string }>('/api/my-plans/:id/attachments', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const plan = planPropio(params.id, sesion.usuario.id)
    if (plan instanceof Response) return plan
    // Un archivo eliminado ya no aparece.
    const lista: AdjuntoPlanDto[] = db.adjuntos
      .filter((a) => a.planUsuarioId === plan.id && a.usuarioId === sesion.usuario.id)
      .sort((a, b) => a.adjuntadoEnUtc.localeCompare(b.adjuntadoEnUtc))
      .flatMap((a) => {
        const d = documentoPropio(a.documentoId, sesion.usuario.id)
        return d
          ? [
              {
                documentoId: d.id,
                nombre: d.nombre,
                tipo: d.tipo,
                tamanoBytes: d.tamanoBytes,
                fechaSubidaUtc: d.fechaSubidaUtc,
                adjuntadoEnUtc: a.adjuntadoEnUtc,
              },
            ]
          : []
      })
    return HttpResponse.json(lista)
  }),

  http.post<{ id: string }>('/api/my-plans/:id/attachments', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request, ROLES)
    if (error) return error
    const body = await leerJson<{ documentoId: unknown }>(request)
    // Un Guid que falta llega como el vacío (queda en 404); uno que no es un Guid no se puede convertir (400).
    if (!body || (body.documentoId !== undefined && !esGuid(body.documentoId)))
      return respuestas.invalida()
    const documentoId = body.documentoId ?? '00000000-0000-0000-0000-000000000000'

    const plan = planPropio(params.id, sesion.usuario.id)
    if (plan instanceof Response) return plan
    if (!enCurso(plan))
      return problema(
        400,
        'La operación no pudo completarse.',
        'Solo puedes adjuntar archivos a un plan en curso.',
        'plan_no_en_curso',
      )
    // RD-06: uno ajeno o eliminado se responde igual que uno que no existe.
    if (!documentoPropio(documentoId, sesion.usuario.id))
      return problema(
        404,
        'No encontrado.',
        'No encontramos ese archivo.',
        'documento_no_encontrado',
      )

    // Los vínculos de archivos ya eliminados se limpian y no ocupan un lugar de los 10.
    db.adjuntos = db.adjuntos.filter(
      (a) =>
        !(
          a.planUsuarioId === plan.id &&
          a.usuarioId === sesion.usuario.id &&
          !documentoPropio(a.documentoId, sesion.usuario.id)
        ),
    )
    const delPlan = db.adjuntos.filter(
      (a) => a.planUsuarioId === plan.id && a.usuarioId === sesion.usuario.id,
    )
    if (delPlan.some((a) => mismoId(a.documentoId, documentoId)))
      return problema(
        400,
        'La operación no pudo completarse.',
        'Ese archivo ya está en este plan.',
        'adjunto_duplicado',
      )
    if (delPlan.length >= MAXIMO_ADJUNTOS_POR_PLAN)
      return problema(
        400,
        'La operación no pudo completarse.',
        `Un plan admite hasta ${MAXIMO_ADJUNTOS_POR_PLAN} archivos.`,
        'limite_adjuntos',
      )

    db.adjuntos.push({
      planUsuarioId: plan.id,
      usuarioId: sesion.usuario.id,
      documentoId: documentoId.toLowerCase(),
      adjuntadoEnUtc: new Date().toISOString(),
    })
    return new HttpResponse(null, { status: 204 })
  }),

  http.delete<{ id: string; documentoId: string }>(
    '/api/my-plans/:id/attachments/:documentoId',
    async ({ request, params }) => {
      await latencia()
      const { sesion, error } = autenticar(request, ROLES)
      if (error) return error
      if (!esGuid(params.documentoId)) return sinCuerpo404()
      const plan = planPropio(params.id, sesion.usuario.id)
      if (plan instanceof Response) return plan
      const adjunto = db.adjuntos.find(
        (a) =>
          a.planUsuarioId === plan.id &&
          mismoId(a.documentoId, params.documentoId) &&
          a.usuarioId === sesion.usuario.id,
      )
      if (!adjunto)
        return problema(
          404,
          'No encontrado.',
          'Ese archivo no está en este plan.',
          'documento_no_encontrado',
        )
      // Quitar el vínculo con el plan en curso pide el mismo permiso que eliminar el archivo; si no, sería la forma de esquivarlo.
      if (enCurso(plan) && !tienePermisoDeArchivo(sesion.usuario, adjunto.documentoId))
        return requierePermiso(
          'Este archivo está en un plan en curso: pide permiso para eliminarlo.',
        )
      db.adjuntos = db.adjuntos.filter((a) => a !== adjunto)
      return new HttpResponse(null, { status: 204 })
    },
  ),
]
