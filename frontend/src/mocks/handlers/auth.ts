import { http, HttpResponse } from 'msw'
import type { UsuarioResponseDto } from '@/features/auth/types'
import { CODIGO_RECUPERACION_DEMO, db, TOKEN_ACTIVACION_DEMO, type UsuarioMock } from '../db'
import {
  autenticar,
  cumplePolitica,
  emitirToken,
  esCorreo,
  esTexto,
  latencia,
  leerJson,
  respuestas,
} from '../http'

const BLOQUEO_MS = 15 * 60 * 1000
const MAX_INTENTOS = 5
const MENSAJE_GENERICO_CORREO = 'Si el correo es válido, recibirás instrucciones en breve.'

const buscarPorCorreo = (correo: string) =>
  db.usuarios.find((u) => u.correo.toLowerCase() === correo.trim().toLowerCase())

export const aDto = (u: UsuarioMock, token: string | null = null): UsuarioResponseDto => ({
  id: u.id,
  nombre: u.nombre,
  correo: u.correo,
  rol: u.rol,
  activo: u.activo,
  token,
})

export const authHandlers = [
  http.post('/api/auth/register', async ({ request }) => {
    await latencia()
    const body = await leerJson<{ nombre: string; correo: string; password: string }>(request)
    if (
      !body ||
      !esTexto(body.nombre, 1, 200) ||
      !esCorreo(body.correo) ||
      !cumplePolitica(body.password)
    ) {
      return respuestas.invalida()
    }
    // RF-CA-01: correo único.
    if (buscarPorCorreo(body.correo)) return respuestas.operacion()

    const nuevo: UsuarioMock = {
      id: crypto.randomUUID(),
      nombre: body.nombre.trim(),
      correo: body.correo.trim().toLowerCase(),
      password: body.password,
      rol: 'Estandar',
      activo: false,
      intentosFallidos: 0,
      bloqueadoHasta: null,
      tokenActivacion: TOKEN_ACTIVACION_DEMO,
      codigoRecuperacion: null,
      sesionesValidasDesde: 0,
    }
    db.usuarios.push(nuevo)
    return HttpResponse.json(aDto(nuevo), { status: 201 })
  }),

  http.post('/api/auth/activate', async ({ request }) => {
    await latencia()
    const token = (await leerJson<{ token: string }>(request))?.token
    if (!esTexto(token, 1, 256)) return respuestas.invalida()
    // Todas las cuentas demo comparten código: se activa la registrada más recientemente.
    const usuario = db.usuarios.findLast((u) => u.tokenActivacion === token.trim() && !u.activo)
    if (!usuario) return respuestas.operacion()
    usuario.activo = true
    usuario.tokenActivacion = null
    return respuestas.mensaje('Cuenta activada correctamente.')
  }),

  http.post('/api/auth/resend-activation', async ({ request }) => {
    await latencia()
    const body = await leerJson<{ correo: string }>(request)
    const usuario = body && esCorreo(body.correo) ? buscarPorCorreo(body.correo) : undefined
    if (usuario && !usuario.activo) usuario.tokenActivacion = TOKEN_ACTIVACION_DEMO
    return respuestas.mensaje(MENSAJE_GENERICO_CORREO)
  }),

  http.post('/api/auth/login', async ({ request }) => {
    await latencia()
    const body = await leerJson<{ email: string; password: string }>(request)
    if (!body || !esCorreo(body.email) || !esTexto(body.password, 1, 1024))
      return respuestas.invalida()

    const usuario = buscarPorCorreo(body.email)
    if (!usuario) return respuestas.noAutorizado()

    const bloqueado = () => usuario.bloqueadoHasta !== null && usuario.bloqueadoHasta > Date.now()

    if (usuario.password === null || usuario.password !== body.password) {
      usuario.intentosFallidos += 1
      if (usuario.intentosFallidos >= MAX_INTENTOS) usuario.bloqueadoHasta = Date.now() + BLOQUEO_MS
      return bloqueado() ? respuestas.operacion() : respuestas.noAutorizado()
    }
    if (bloqueado() || !usuario.activo) return respuestas.operacion()

    usuario.intentosFallidos = 0
    usuario.bloqueadoHasta = null
    return HttpResponse.json(aDto(usuario, emitirToken(usuario)))
  }),

  http.get('/api/auth/me', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    return HttpResponse.json({
      id: sesion.usuario.id,
      correo: sesion.usuario.correo,
      rol: sesion.usuario.rol,
    })
  }),

  http.post('/api/auth/logout', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    db.tokensRevocados.add(sesion.jti)
    return respuestas.mensaje('Sesión cerrada.')
  }),

  ...['recuperar-password', 'forgot-password'].map((ruta) =>
    http.post(`/api/auth/${ruta}`, async ({ request }) => {
      await latencia()
      const body = await leerJson<{ correo: string }>(request)
      const usuario = body && esCorreo(body.correo) ? buscarPorCorreo(body.correo) : undefined
      if (usuario) usuario.codigoRecuperacion = CODIGO_RECUPERACION_DEMO
      // RF-CA-09: misma respuesta exista o no el correo.
      return respuestas.mensaje(MENSAJE_GENERICO_CORREO)
    }),
  ),

  ...['restablecer-password', 'reset-password'].map((ruta) =>
    http.post(`/api/auth/${ruta}`, async ({ request }) => {
      await latencia()
      const body = await leerJson<{ correo: string; codigo: string; nuevaPassword: string }>(
        request,
      )
      if (
        !body ||
        !esCorreo(body.correo) ||
        !esTexto(body.codigo, 1, 64) ||
        !cumplePolitica(body.nuevaPassword)
      ) {
        return respuestas.invalida()
      }
      const usuario = buscarPorCorreo(body.correo)
      if (
        !usuario ||
        !usuario.codigoRecuperacion ||
        usuario.codigoRecuperacion !== body.codigo.trim()
      ) {
        return respuestas.operacion()
      }
      usuario.password = body.nuevaPassword
      usuario.codigoRecuperacion = null
      usuario.sesionesValidasDesde = Date.now()
      return respuestas.mensaje('Contraseña restablecida.')
    }),
  ),

  http.post('/api/auth/cambiar-password', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const body = await leerJson<{ actual: string; nueva: string }>(request)
    if (!body || !esTexto(body.actual, 1, 1024) || !cumplePolitica(body.nueva))
      return respuestas.invalida()
    if (sesion.usuario.password !== body.actual) return respuestas.noAutorizado()
    sesion.usuario.password = body.nueva
    // RF-CA-12: invalida las sesiones previas.
    sesion.usuario.sesionesValidasDesde = Date.now()
    return respuestas.mensaje('Contraseña actualizada.')
  }),
]
