import { describe, expect, it } from 'vitest'
import type { CorreoColaDto } from '@/features/cola-correos/types'
import type { NotificacionDto } from '@/features/notificaciones/types'
import { iniciarSesionComo } from '@/test/render'
import { db } from './db'

// Notificaciones (/api/notifications) y cola de correos (/api/admin/email-queue): la API simulada responde
// como NotificationsController y AdminColaCorreosController, y los avisos nacen como en NotificadorPermisosAdapter.
const url = (ruta: string) => `${window.location.origin}${ruta}`

async function llamar(
  ruta: string,
  opciones: { metodo?: string; token?: string; body?: unknown } = {},
) {
  const headers: Record<string, string> = { 'Content-Type': 'application/json' }
  if (opciones.token) headers.Authorization = `Bearer ${opciones.token}`
  return fetch(url(ruta), {
    method: opciones.metodo ?? 'GET',
    headers,
    body: opciones.body === undefined ? undefined : JSON.stringify(opciones.body),
  })
}

const notificaciones = async (token: string, consulta = ''): Promise<NotificacionDto[]> =>
  (await llamar(`/api/notifications${consulta}`, { token })).json()
const cola = async (token: string, consulta = ''): Promise<CorreoColaDto[]> =>
  (await llamar(`/api/admin/email-queue${consulta}`, { token })).json()

const pedir = async (token: string, permisoSolicitado: string): Promise<string> =>
  (
    await (
      await llamar('/api/permissions', { metodo: 'POST', token, body: { permisoSolicitado } })
    ).json()
  ).id
const resolver = (token: string, id: string, accion: 'approve' | 'reject', motivoRechazo = 'No.') =>
  llamar(`/api/permissions/${id}/${accion}`, { metodo: 'POST', token, body: { motivoRechazo } })

describe('notificaciones: autenticación y alcance', () => {
  it('sin token todo responde 401', async () => {
    for (const [metodo, ruta] of [
      ['GET', '/api/notifications'],
      ['POST', '/api/notifications/5a4b3c2d-1e0f-4a9b-8c7d-6e5f4a3b2c01/read'],
      ['GET', '/api/notifications/preferences'],
      ['PUT', '/api/notifications/preferences'],
      ['GET', '/api/admin/email-queue'],
      ['POST', '/api/admin/email-queue/process'],
    ]) {
      expect((await llamar(ruta, { metodo })).status, ruta).toBe(401)
    }
  })

  it('crear una solicitud avisa a TODOS los Administradores activos y a nadie más', async () => {
    // Un segundo Administrador activo y un tercero desactivado.
    db.usuarios.find((u) => u.correo === 'otro@demo.com')!.rol = 'Administrador'
    db.usuarios.find((u) => u.correo === 'inactiva@demo.com')!.rol = 'Administrador'
    const estandar = iniciarSesionComo('estandar')
    await pedir(estandar, 'Habits.AdvancedReconfig')

    for (const quien of ['admin', 'otro'] as const) {
      const lista = await notificaciones(iniciarSesionComo(quien))
      expect(lista).toHaveLength(1)
      expect(lista[0]).toMatchObject({
        tipo: 'SolicitudCreada',
        titulo: 'Nueva solicitud de permiso',
        leida: false,
        leidaEnUtc: null,
      })
      expect(lista[0].mensaje).toContain('estandar@demo.com')
      expect(lista[0].mensaje).toContain('«Habits.AdvancedReconfig»')
    }
    expect(await notificaciones(estandar)).toEqual([])
    expect(db.notificaciones.some((n) => n.usuarioId === db.usuarios[3].id)).toBe(false)
  })

  it('aprobar avisa solo al solicitante; rechazar lo hace con el motivo', async () => {
    const estandar = iniciarSesionComo('estandar')
    const admin = iniciarSesionComo('admin')
    const aprobada = await pedir(estandar, 'Habits.AdvancedReconfig')
    const rechazada = await pedir(estandar, 'Habits.Otra')
    const antesDelAdmin = (await notificaciones(admin)).length

    await resolver(admin, aprobada, 'approve')
    await resolver(admin, rechazada, 'reject', 'Falta justificar.')

    const propias = (await notificaciones(estandar)).filter((n) => n.tipo === 'SolicitudResuelta')
    // La más reciente primero: el rechazo y luego la aprobación.
    expect(propias.map((n) => n.titulo)).toEqual(['Solicitud rechazada', 'Solicitud aprobada'])
    expect(propias[0].mensaje).toContain('rechazó')
    expect(propias[0].mensaje).toContain('Motivo: Falta justificar.')
    expect(propias[1].mensaje).toContain('aprobó')
    expect(propias[1].mensaje).toContain('«Habits.AdvancedReconfig»')
    // El Administrador que resuelve no recibe aviso de la resolución.
    expect(await notificaciones(admin)).toHaveLength(antesDelAdmin)
    // Otra persona tampoco.
    expect(await notificaciones(iniciarSesionComo('otro'))).toEqual([])
  })

  it('cada persona ve solo las suyas, de la más reciente a la más antigua', async () => {
    const estandar = iniciarSesionComo('estandar')
    const admin = iniciarSesionComo('admin')
    await pedir(estandar, 'Habits.Primera')
    await pedir(estandar, 'Habits.Segunda')
    const lista = await notificaciones(admin)
    expect(lista.map((n) => n.mensaje.match(/«(.+)»/)?.[1])).toEqual([
      'Habits.Segunda',
      'Habits.Primera',
    ])
    expect(Object.keys(lista[0]).sort()).toEqual(
      ['creadaEnUtc', 'id', 'leida', 'leidaEnUtc', 'mensaje', 'tipo', 'titulo'].sort(),
    )
  })
})

describe('notificaciones: marcar como leída', () => {
  const crearUnaParaElAdmin = async () => {
    await pedir(iniciarSesionComo('estandar'), 'Habits.AdvancedReconfig')
    const admin = iniciarSesionComo('admin')
    return { admin, id: (await notificaciones(admin))[0].id }
  }

  it('POST {id}/read responde 204 y soloNoLeidas deja de mostrarla', async () => {
    const { admin, id } = await crearUnaParaElAdmin()
    expect(await notificaciones(admin, '?soloNoLeidas=true')).toHaveLength(1)
    const r = await llamar(`/api/notifications/${id}/read`, { metodo: 'POST', token: admin })
    expect(r.status).toBe(204)
    expect(await r.text()).toBe('')
    expect(await notificaciones(admin, '?soloNoLeidas=true')).toEqual([])
    const todas = await notificaciones(admin, '?soloNoLeidas=false')
    expect(todas).toHaveLength(1)
    expect(todas[0]).toMatchObject({ leida: true, leidaEnUtc: expect.any(String) })
  })

  it('soloNoLeidas sin valor es false; un valor que no es bool es 400', async () => {
    const { admin, id } = await crearUnaParaElAdmin()
    await llamar(`/api/notifications/${id}/read`, { metodo: 'POST', token: admin })
    expect(await notificaciones(admin)).toHaveLength(1)
    const r = await llamar('/api/notifications?soloNoLeidas=quizas', { token: admin })
    expect(r.status).toBe(400)
  })

  it('la de otra persona es 403 con ProblemDetails y no se marca', async () => {
    const { id } = await crearUnaParaElAdmin()
    const r = await llamar(`/api/notifications/${id}/read`, {
      metodo: 'POST',
      token: iniciarSesionComo('estandar'),
    })
    expect(r.status).toBe(403)
    expect(await r.json()).toMatchObject({ title: 'No autorizado.', status: 403 })
    expect(db.notificaciones.find((n) => n.id === id)?.leida).toBe(false)
  })

  it('una que no existe es 404 con codigo notificacion_no_encontrada; un id que no es GUID, 404 sin cuerpo', async () => {
    const token = iniciarSesionComo('estandar')
    const inexistente = await llamar(
      '/api/notifications/11111111-2222-4333-8444-555555555555/read',
      {
        metodo: 'POST',
        token,
      },
    )
    expect(inexistente.status).toBe(404)
    expect(await inexistente.json()).toMatchObject({
      title: 'No encontrado.',
      codigo: 'notificacion_no_encontrada',
    })
    const sinGuid = await llamar('/api/notifications/no-es-guid/read', { metodo: 'POST', token })
    expect(sinGuid.status).toBe(404)
    expect(await sinGuid.text()).toBe('')
  })
})

describe('notificaciones: preferencias de correo', () => {
  it('el correo está activo por defecto', async () => {
    const r = await llamar('/api/notifications/preferences', { token: iniciarSesionComo('admin') })
    expect(r.status).toBe(200)
    expect(await r.json()).toEqual({ correoActivo: true })
  })

  it('PUT exige correoActivo booleano: sin él (o con otro tipo) es 400 y no cambia nada', async () => {
    const token = iniciarSesionComo('admin')
    for (const body of [
      {},
      { correoActivo: null },
      { correoActivo: 'false' },
      { correoActivo: 0 },
    ]) {
      const r = await llamar('/api/notifications/preferences', { metodo: 'PUT', token, body })
      expect(r.status, JSON.stringify(body)).toBe(400)
    }
    const sinCuerpo = await llamar('/api/notifications/preferences', { metodo: 'PUT', token })
    expect(sinCuerpo.status).toBe(400)
    expect(db.preferenciasCorreo.size).toBe(0)
  })

  it('PUT guarda la preferencia solo de quien la pide y responde con ella', async () => {
    const admin = iniciarSesionComo('admin')
    const guardada = await llamar('/api/notifications/preferences', {
      metodo: 'PUT',
      token: admin,
      body: { correoActivo: false },
    })
    expect(guardada.status).toBe(200)
    expect(await guardada.json()).toEqual({ correoActivo: false })
    expect(await (await llamar('/api/notifications/preferences', { token: admin })).json()).toEqual(
      {
        correoActivo: false,
      },
    )
    // Otra persona conserva el valor por defecto.
    const otra = iniciarSesionComo('estandar')
    expect(await (await llamar('/api/notifications/preferences', { token: otra })).json()).toEqual({
      correoActivo: true,
    })
  })

  it('con el correo desactivado la notificación interna sigue y no se encola nada (RF-NOT-07)', async () => {
    const admin = iniciarSesionComo('admin')
    await llamar('/api/notifications/preferences', {
      metodo: 'PUT',
      token: admin,
      body: { correoActivo: false },
    })
    await pedir(iniciarSesionComo('estandar'), 'Habits.AdvancedReconfig')
    expect(await notificaciones(admin)).toHaveLength(1)
    expect(await cola(admin)).toEqual([])
  })
})

describe('cola de correos (solo Administrador)', () => {
  const MIN = 60_000
  const correoQueFalla = (): CorreoColaDto => ({
    id: crypto.randomUUID(),
    destinatario: 'alguien@fallo.test',
    asunto: 'Aviso',
    estado: 'Pendiente',
    intentos: 0,
    fechaCreacion: new Date().toISOString(),
    fechaUltimoIntento: null,
    fechaEnvio: null,
    ultimoError: null,
  })
  const procesar = async (token: string) =>
    (await llamar('/api/admin/email-queue/process', { metodo: 'POST', token })).json()

  it('un Estándar recibe 403 en ambos endpoints', async () => {
    const token = iniciarSesionComo('estandar')
    expect((await llamar('/api/admin/email-queue', { token })).status).toBe(403)
    expect((await llamar('/api/admin/email-queue/process', { metodo: 'POST', token })).status).toBe(
      403,
    )
  })

  it('la notificación encola un correo Pendiente para quien la recibe, sin el cuerpo', async () => {
    await pedir(iniciarSesionComo('estandar'), 'Habits.AdvancedReconfig')
    const lista = await cola(iniciarSesionComo('admin'))
    expect(lista).toHaveLength(1)
    expect(lista[0]).toMatchObject({
      destinatario: 'admin@demo.com',
      asunto: 'Nueva solicitud de permiso',
      estado: 'Pendiente',
      intentos: 0,
      fechaUltimoIntento: null,
      fechaEnvio: null,
      ultimoError: null,
    })
    expect(Object.keys(lista[0]).sort()).toEqual(
      [
        'asunto',
        'destinatario',
        'estado',
        'fechaCreacion',
        'fechaEnvio',
        'fechaUltimoIntento',
        'id',
        'intentos',
        'ultimoError',
      ].sort(),
    )
  })

  it('?estado filtra por nombre (sin mayúsculas); uno inválido, o un número, es 400', async () => {
    await pedir(iniciarSesionComo('estandar'), 'Habits.AdvancedReconfig')
    const admin = iniciarSesionComo('admin')
    expect(await cola(admin, '?estado=pendiente')).toHaveLength(1)
    expect(await cola(admin, '?estado=Enviado')).toEqual([])
    expect(await cola(admin, '?estado=')).toHaveLength(1)
    for (const estado of ['Otro', '1']) {
      expect(
        (await llamar(`/api/admin/email-queue?estado=${estado}`, { token: admin })).status,
      ).toBe(400)
    }
  })

  it('POST process envía los Pendiente y responde {enviados, fallidos, recuperados}', async () => {
    await pedir(iniciarSesionComo('estandar'), 'Habits.AdvancedReconfig')
    const admin = iniciarSesionComo('admin')
    expect(await procesar(admin)).toEqual({ enviados: 1, fallidos: 0, recuperados: 0 })
    const [correo] = await cola(admin)
    expect(correo).toMatchObject({ estado: 'Enviado', intentos: 0, ultimoError: null })
    expect(correo.fechaEnvio).toEqual(expect.any(String))
    expect(correo.fechaUltimoIntento).toEqual(expect.any(String))
    // Otra pasada no encuentra nada que enviar.
    expect(await procesar(admin)).toEqual({ enviados: 0, fallidos: 0, recuperados: 0 })
  })

  it('un fallo suma un intento y se reintenta tras la espera hasta el máximo de 3', async () => {
    const admin = iniciarSesionComo('admin')
    const correo = correoQueFalla()
    db.correos.push(correo)

    expect(await procesar(admin)).toEqual({ enviados: 0, fallidos: 1, recuperados: 0 })
    expect(correo).toMatchObject({ estado: 'Fallido', intentos: 1 })
    expect(correo.ultimoError).toEqual(expect.any(String))

    // Sin esperar el backoff (1 min × 2^intentos) no se reintenta.
    expect(await procesar(admin)).toEqual({ enviados: 0, fallidos: 0, recuperados: 0 })
    expect(correo.intentos).toBe(1)

    for (const esperado of [2, 3]) {
      correo.fechaUltimoIntento = new Date(Date.now() - 10 * MIN).toISOString()
      expect(await procesar(admin)).toEqual({ enviados: 0, fallidos: 1, recuperados: 0 })
      expect(correo.intentos).toBe(esperado)
    }

    // Agotó sus 3 intentos: se queda Fallido para siempre.
    correo.fechaUltimoIntento = new Date(Date.now() - 60 * MIN).toISOString()
    expect(await procesar(admin)).toEqual({ enviados: 0, fallidos: 0, recuperados: 0 })
    expect(correo).toMatchObject({ estado: 'Fallido', intentos: 3 })
    expect(await cola(admin, '?estado=Fallido')).toHaveLength(1)
  })

  it('devuelve como máximo 200, del más reciente al más antiguo', async () => {
    const admin = iniciarSesionComo('admin')
    const base = Date.now()
    for (let i = 0; i < 205; i++) {
      db.correos.push({
        ...correoQueFalla(),
        asunto: `Correo ${i}`,
        fechaCreacion: new Date(base - i * 1000).toISOString(),
      })
    }
    const lista = await cola(admin)
    expect(lista).toHaveLength(200)
    expect(lista[0].asunto).toBe('Correo 0')
    expect(lista[199].asunto).toBe('Correo 199')
  })
})
