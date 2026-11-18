import { http, HttpResponse } from 'msw'
import { db } from '../db'
import { autenticar, esGuid, latencia, leerJson, problema, respuestas } from '../http'

// El middleware responde 404 con el código estable (NotificacionNoEncontradaException).
const noEncontrada = () =>
  problema(404, 'No encontrado.', 'La notificación no existe.', 'notificacion_no_encontrada')

// ValidationProblemDetails de [ApiController]: el valor de la consulta no es un bool.
const boolInvalido = () =>
  HttpResponse.json(
    {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { soloNoLeidas: ['The value is not valid.'] },
    },
    { status: 400, headers: { 'Content-Type': 'application/problem+json' } },
  )

export const notificacionesHandlers = [
  http.get('/api/notifications', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const valor = new URL(request.url).searchParams.get('soloNoLeidas')
    if (valor !== null && !/^(true|false)$/i.test(valor)) return boolInvalido()
    const soloNoLeidas = valor?.toLowerCase() === 'true'
    // Solo las propias, de la más reciente a la más antigua (db.notificaciones ya va en ese orden).
    const propias = db.notificaciones
      .filter((n) => n.usuarioId === sesion.usuario.id && (!soloNoLeidas || !n.leida))
      .map((n) => ({
        id: n.id,
        tipo: n.tipo,
        titulo: n.titulo,
        mensaje: n.mensaje,
        creadaEnUtc: n.creadaEnUtc,
        leida: n.leida,
        leidaEnUtc: n.leidaEnUtc,
      }))
    return HttpResponse.json(propias)
  }),

  http.post<{ id: string }>('/api/notifications/:id/read', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    // La ruta {id:guid} no atiende otro formato: 404 sin cuerpo.
    if (!esGuid(params.id)) return new HttpResponse(null, { status: 404 })
    const notificacion = db.notificaciones.find((n) => n.id === params.id)
    if (!notificacion) return noEncontrada()
    // Notificacion.MarcarLeida lanza UnauthorizedAccessException con la de otra persona: 403 con cuerpo.
    if (notificacion.usuarioId !== sesion.usuario.id) return respuestas.ajeno()
    if (!notificacion.leida) {
      notificacion.leida = true
      notificacion.leidaEnUtc = new Date().toISOString()
    }
    return new HttpResponse(null, { status: 204 })
  }),

  http.get('/api/notifications/preferences', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    return HttpResponse.json({ correoActivo: db.preferenciasCorreo.get(sesion.usuario.id) ?? true })
  }),

  http.put('/api/notifications/preferences', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const body = await leerJson<{ correoActivo: unknown }>(request)
    // [Required] bool?: sin `correoActivo` (o con otro tipo) es 400, no un «false» silencioso.
    if (!body || typeof body.correoActivo !== 'boolean') return respuestas.invalida()
    db.preferenciasCorreo.set(sesion.usuario.id, body.correoActivo)
    return HttpResponse.json({ correoActivo: body.correoActivo })
  }),
]
