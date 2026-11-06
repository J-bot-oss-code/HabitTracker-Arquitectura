import { http, HttpResponse } from 'msw'
import type { UsuarioResponseDto } from '@/features/auth/types'
import { leerTokenDemo } from '@/features/auth/google/tokenDemo'
import { VERSION_PRIVACIDAD, VERSION_TERMINOS } from '@/features/publico/legal'
import { CABECERA_CSRF } from '@/shared/api/sesion'
import { auditar } from '../auditoria'
import { CODIGO_RECUPERACION_DEMO, db, TOKEN_ACTIVACION_DEMO, type UsuarioMock } from '../db'
import {
  autenticar,
  cumplePolitica,
  emitirToken,
  esCorreo,
  esTexto,
  latencia,
  leerJson,
  problema,
  respuestas,
} from '../http'

const BLOQUEO_MS = 15 * 60 * 1000
const MAX_INTENTOS = 5
const MENSAJE_GENERICO_CORREO = 'Si el correo es válido, recibirás instrucciones en breve.'

const buscarPorCorreo = (correo: string) =>
  db.usuarios.find((u) => u.correo.toLowerCase() === correo.trim().toLowerCase())

// ConsentimientoService.RequiereAceptarDocumentosAsync: nunca aceptó, o su última aceptación no es de las
// versiones vigentes.
function requiereAceptarDocumentos(usuarioId: string): boolean {
  const vigente = db.consentimientos.get(usuarioId)
  return (
    !vigente ||
    vigente.terminosVersion !== VERSION_TERMINOS ||
    vigente.privacidadVersion !== VERSION_PRIVACIDAD
  )
}

export const aDto = (u: UsuarioMock, token: string | null = null): UsuarioResponseDto => ({
  id: u.id,
  nombre: u.nombre,
  correo: u.correo,
  rol: u.rol,
  activo: u.activo,
  token,
  requiereAceptarDocumentos: requiereAceptarDocumentos(u.id),
  bajaProgramadaPara: null,
})

// Abre la sesión como el backend: token de acceso en el cuerpo y cookie HttpOnly con el refresh token.
function abrirSesion(usuario: UsuarioMock): string {
  db.refresh = { usuarioId: usuario.id, valor: crypto.randomUUID() }
  return emitirToken(usuario)
}

// IGestorSesiones.RevocarTodasAsync: corta los accesos emitidos y el refresh token.
function revocarSesiones(usuario: UsuarioMock) {
  usuario.sesionesValidasDesde = Date.now()
  if (db.refresh?.usuarioId === usuario.id) db.refresh = null
}

function consentimientosValidos(
  c: unknown,
): c is { terminosVersion: string; privacidadVersion: string; marketing: boolean } {
  if (!c || typeof c !== 'object') return false
  const { terminosVersion, privacidadVersion, marketing } = c as Record<string, unknown>
  return (
    terminosVersion === VERSION_TERMINOS &&
    privacidadVersion === VERSION_PRIVACIDAD &&
    typeof marketing === 'boolean'
  )
}

const tokenExternoInvalido = () =>
  problema(
    401,
    'Credenciales incorrectas.',
    'No se pudo verificar la cuenta de Google.',
    'token_externo_invalido',
  )

// Las mismas reglas del inicio de sesión con contraseña para una cuenta ya vinculada (RF-CA-15, 19, 20).
function rechazoDeEntrada(usuario: UsuarioMock) {
  if (usuario.bloqueadoHasta !== null && usuario.bloqueadoHasta > Date.now()) {
    return respuestas.codigo('cuenta_bloqueada', 'La cuenta está bloqueada temporalmente.')
  }
  if (!usuario.activo) {
    return usuario.tokenActivacion !== null
      ? respuestas.codigo('cuenta_no_activa', 'La cuenta todavía no está activada.')
      : respuestas.codigo('cuenta_desactivada', 'La cuenta fue desactivada por un administrador.')
  }
  return null
}

export const authHandlers = [
  http.post('/api/auth/register', async ({ request }) => {
    await latencia()
    const body = await leerJson<{
      nombre: string
      correo: string
      password: string
      consentimientos: unknown
    }>(request)
    if (!body || !esTexto(body.nombre, 1, 200) || !esCorreo(body.correo)) {
      return respuestas.invalida()
    }
    if (!cumplePolitica(body.password)) {
      return respuestas.codigo('contrasena_no_valida', 'La contraseña no cumple la política.')
    }
    // Sin la aceptación de las versiones vigentes de los documentos legales no se crea la cuenta.
    if (!consentimientosValidos(body.consentimientos)) {
      return respuestas.codigo('consentimiento_no_valido', 'Falta aceptar los documentos vigentes.')
    }
    // RF-CA-01: correo único.
    if (buscarPorCorreo(body.correo)) {
      return respuestas.codigo('correo_ya_registrado', 'Ya existe una cuenta con ese correo.')
    }

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
    // El usuario y su consentimiento se guardan juntos (IUsuarioRepository.SaveAsync).
    db.consentimientos.set(nuevo.id, {
      terminosVersion: body.consentimientos.terminosVersion,
      privacidadVersion: body.consentimientos.privacidadVersion,
      aceptadoEn: new Date().toISOString(),
      datosSalud: false,
      marketing: body.consentimientos.marketing,
      compartirCumplimiento: false,
      estadisticasAnonimas: false,
    })
    auditar({ actor: nuevo.correo, accion: 'Registro', entidad: 'Usuario', entidadId: nuevo.id })
    return HttpResponse.json(aDto(nuevo), { status: 201 })
  }),

  // El código de 6 dígitos no es único entre usuarios: siempre va con el correo.
  http.post('/api/auth/activate', async ({ request }) => {
    await latencia()
    const body = await leerJson<{ correo: string; codigo: string }>(request)
    if (
      !body ||
      !esCorreo(body.correo) ||
      typeof body.codigo !== 'string' ||
      !/^\d{6}$/.test(body.codigo)
    ) {
      return respuestas.invalida()
    }
    const usuario = buscarPorCorreo(body.correo)
    // Mismo rechazo exista o no el correo, esté activo o el código sea otro.
    if (!usuario || usuario.activo || usuario.tokenActivacion !== body.codigo) {
      return respuestas.codigo(
        'codigo_invalido',
        'El código es incorrecto, ya fue usado o ha expirado.',
      )
    }
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

    // Cuenta de Google sin contraseña: 401 genérico y sin contar para el bloqueo (no hay clave que adivinar).
    if (usuario.password === null) return respuestas.noAutorizado()

    if (usuario.password !== body.password) {
      usuario.intentosFallidos += 1
      if (usuario.intentosFallidos >= MAX_INTENTOS) usuario.bloqueadoHasta = Date.now() + BLOQUEO_MS
      return bloqueado()
        ? respuestas.codigo('cuenta_bloqueada', 'La cuenta está bloqueada temporalmente.')
        : respuestas.noAutorizado()
    }
    // Con la clave correcta el backend dice por qué no se puede entrar (RF-CA-15, RF-CA-19, RF-CA-20).
    if (bloqueado()) {
      return respuestas.codigo('cuenta_bloqueada', 'La cuenta está bloqueada temporalmente.')
    }
    if (!usuario.activo) {
      return usuario.tokenActivacion !== null
        ? respuestas.codigo('cuenta_no_activa', 'La cuenta todavía no está activada.')
        : respuestas.codigo('cuenta_desactivada', 'La cuenta fue desactivada por un administrador.')
    }

    usuario.intentosFallidos = 0
    usuario.bloqueadoHasta = null
    auditar({ actor: usuario.correo, accion: 'InicioSesion', entidad: 'Sesión' })
    return HttpResponse.json(aDto(usuario, abrirSesion(usuario)))
  }),

  // POST /api/auth/refresh: se autentica solo con la cookie, así que exige la cabecera anti-CSRF. Rota el
  // refresh token y entrega un token de acceso nuevo.
  http.post('/api/auth/refresh', async ({ request }) => {
    await latencia()
    if (request.headers.get(CABECERA_CSRF) !== '1') {
      return problema(
        403,
        'Solicitud no permitida.',
        'Falta la cabecera de protección de la solicitud.',
        'csrf_requerido',
      )
    }
    const sesion = db.refresh
    const usuario = sesion && db.usuarios.find((u) => u.id === sesion.usuarioId)
    const bloqueado = usuario?.bloqueadoHasta != null && usuario.bloqueadoHasta > Date.now()
    if (!sesion || !usuario || !usuario.activo || bloqueado) {
      db.refresh = null
      return problema(
        401,
        'Sesión inválida.',
        'La sesión ya no es válida. Inicie sesión nuevamente.',
        'sesion_invalida',
      )
    }
    return HttpResponse.json(aDto(usuario, abrirSesion(usuario)))
  }),

  http.get('/api/auth/me', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    return HttpResponse.json({
      id: sesion.usuario.id,
      nombre: sesion.usuario.nombre,
      correo: sesion.usuario.correo,
      rol: sesion.usuario.rol,
      requiereAceptarDocumentos: requiereAceptarDocumentos(sesion.usuario.id),
    })
  }),

  http.post('/api/auth/logout', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    db.tokensRevocados.add(sesion.jti)
    if (db.refresh?.usuarioId === sesion.usuario.id) db.refresh = null
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
      if (!body || !esCorreo(body.correo) || !esTexto(body.codigo, 1, 64)) {
        return respuestas.invalida()
      }
      if (!cumplePolitica(body.nuevaPassword)) {
        return respuestas.codigo('contrasena_no_valida', 'La contraseña no cumple la política.')
      }
      const usuario = buscarPorCorreo(body.correo)
      if (
        !usuario ||
        !usuario.codigoRecuperacion ||
        usuario.codigoRecuperacion !== body.codigo.trim()
      ) {
        return respuestas.codigo(
          'codigo_invalido',
          'El código es incorrecto, ya fue usado o ha expirado.',
        )
      }
      usuario.password = body.nuevaPassword
      usuario.codigoRecuperacion = null
      revocarSesiones(usuario)
      return respuestas.mensaje('Contraseña restablecida.')
    }),
  ),

  http.post('/api/auth/cambiar-password', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const body = await leerJson<{ actual: string; nueva: string }>(request)
    if (!body || !esTexto(body.actual, 1, 1024)) return respuestas.invalida()
    if (!cumplePolitica(body.nueva)) {
      return respuestas.codigo('contrasena_no_valida', 'La contraseña no cumple la política.')
    }
    if (sesion.usuario.password === null) {
      return respuestas.codigo('sin_contrasena', 'La cuenta no tiene contraseña.')
    }
    // 400 y no 401/403: un 401 cerraría la sesión del cliente por un simple error de tecleo.
    if (sesion.usuario.password !== body.actual) {
      return respuestas.codigo('clave_actual_incorrecta', 'La contraseña actual no coincide.')
    }
    sesion.usuario.password = body.nueva
    // RF-CA-12: invalida las sesiones previas.
    revocarSesiones(sesion.usuario)
    return respuestas.mensaje('Contraseña actualizada.')
  }),

  // ---------------- Inicio con Google (el "ID token" de la demostración trae la identidad) ----------------

  http.post('/api/auth/google', async ({ request }) => {
    await latencia()
    const body = await leerJson<{ idToken: string; consentimientos: unknown }>(request)
    if (!body || !esTexto(body.idToken, 1, 4096)) return respuestas.invalida()
    // Token falso, de otra aplicación o con el correo sin verificar: 401 sin decir cuál.
    const identidad = leerTokenDemo(body.idToken)
    if (!identidad || !identidad.correoVerificado) return tokenExternoInvalido()

    // 1. Identidad ya vinculada: inicio de sesión normal, con las reglas del login.
    const vinculado = db.usuarios.find(
      (u) => db.vinculosGoogle.get(u.id)?.subject === identidad.sub,
    )
    if (vinculado) {
      const rechazo = rechazoDeEntrada(vinculado)
      if (rechazo) return rechazo
      auditar({ actor: vinculado.correo, accion: 'InicioSesion', entidad: 'Sesión' })
      return HttpResponse.json(aDto(vinculado, abrirSesion(vinculado)))
    }

    // 2. El correo ya tiene cuenta: no se vincula sola, hay que confirmar la contraseña.
    if (buscarPorCorreo(identidad.correo)) {
      return problema(
        409,
        'Vinculación requerida.',
        'Ya existe una cuenta con este correo. Confirma tu contraseña para vincular Google.',
        'vinculacion_requerida',
      )
    }

    // 3. Correo nuevo: exige los documentos vigentes y crea la cuenta ya activa y sin contraseña.
    if (body.consentimientos === undefined || body.consentimientos === null) {
      return respuestas.codigo(
        'consentimiento_requerido',
        'Para crear la cuenta debes aceptar los Términos y la Política de privacidad vigentes.',
      )
    }
    if (!consentimientosValidos(body.consentimientos)) {
      return respuestas.codigo('consentimiento_no_valido', 'Falta aceptar los documentos vigentes.')
    }
    const nuevo: UsuarioMock = {
      id: crypto.randomUUID(),
      nombre: identidad.nombre || identidad.correo.split('@')[0],
      correo: identidad.correo,
      password: null,
      rol: 'Estandar',
      activo: true,
      intentosFallidos: 0,
      bloqueadoHasta: null,
      tokenActivacion: null,
      codigoRecuperacion: null,
      sesionesValidasDesde: 0,
    }
    db.usuarios.push(nuevo)
    db.vinculosGoogle.set(nuevo.id, {
      subject: identidad.sub,
      vinculadoEn: new Date().toISOString(),
    })
    db.consentimientos.set(nuevo.id, {
      terminosVersion: body.consentimientos.terminosVersion,
      privacidadVersion: body.consentimientos.privacidadVersion,
      aceptadoEn: new Date().toISOString(),
      datosSalud: false,
      marketing: body.consentimientos.marketing,
      compartirCumplimiento: false,
      estadisticasAnonimas: false,
    })
    auditar({ actor: nuevo.correo, accion: 'Registro', entidad: 'Usuario', entidadId: nuevo.id })
    return HttpResponse.json(aDto(nuevo, abrirSesion(nuevo)), { status: 201 })
  }),

  // Vincular Google a la cuenta con contraseña de ese correo, confirmando la contraseña (cuenta para el bloqueo).
  http.post('/api/auth/google/vincular', async ({ request }) => {
    await latencia()
    const body = await leerJson<{ idToken: string; password: string }>(request)
    if (!body || !esTexto(body.idToken, 1, 4096) || !esTexto(body.password, 1, 1024)) {
      return respuestas.invalida()
    }
    const identidad = leerTokenDemo(body.idToken)
    if (!identidad || !identidad.correoVerificado) return tokenExternoInvalido()

    const usuario = buscarPorCorreo(identidad.correo)
    // Sin cuenta con contraseña no hay nada que confirmar: mismo 401 que una clave incorrecta.
    if (!usuario || usuario.password === null) return respuestas.noAutorizado()

    if (usuario.password !== body.password) {
      usuario.intentosFallidos += 1
      if (usuario.intentosFallidos >= MAX_INTENTOS) usuario.bloqueadoHasta = Date.now() + BLOQUEO_MS
      return usuario.bloqueadoHasta !== null && usuario.bloqueadoHasta > Date.now()
        ? respuestas.codigo('cuenta_bloqueada', 'La cuenta está bloqueada temporalmente.')
        : respuestas.noAutorizado()
    }
    if (usuario.bloqueadoHasta !== null && usuario.bloqueadoHasta > Date.now()) {
      return respuestas.codigo('cuenta_bloqueada', 'La cuenta está bloqueada temporalmente.')
    }
    const pendiente = !usuario.activo && usuario.tokenActivacion !== null
    if (!usuario.activo && !pendiente) {
      return respuestas.codigo(
        'cuenta_desactivada',
        'La cuenta fue desactivada por un administrador.',
      )
    }

    const otraCuenta = db.usuarios.find(
      (u) => u.id !== usuario.id && db.vinculosGoogle.get(u.id)?.subject === identidad.sub,
    )
    const actual = db.vinculosGoogle.get(usuario.id)
    if (otraCuenta || (actual && actual.subject !== identidad.sub)) {
      return respuestas.codigo(
        'proveedor_ya_vinculado',
        'La identidad de Google ya está vinculada.',
      )
    }
    if (!actual) {
      db.vinculosGoogle.set(usuario.id, {
        subject: identidad.sub,
        vinculadoEn: new Date().toISOString(),
      })
    }
    // Google acaba de verificar el correo: una cuenta que esperaba su código queda activa.
    if (pendiente) {
      usuario.activo = true
      usuario.tokenActivacion = null
    }
    usuario.intentosFallidos = 0
    usuario.bloqueadoHasta = null
    return HttpResponse.json(aDto(usuario, abrirSesion(usuario)))
  }),

  http.get('/api/auth/vinculos', async ({ request }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    const vinculo = db.vinculosGoogle.get(sesion.usuario.id)
    return HttpResponse.json({
      tienePassword: sesion.usuario.password !== null,
      vinculos: vinculo ? [{ proveedor: 'google', vinculadoEn: vinculo.vinculadoEn }] : [],
    })
  }),

  // Solo se puede desvincular si la cuenta tiene contraseña: si no, quedaría sin forma de entrar.
  http.delete('/api/auth/vinculos/:proveedor', async ({ request, params }) => {
    await latencia()
    const { sesion, error } = autenticar(request)
    if (error) return error
    if (params.proveedor !== 'google' || !db.vinculosGoogle.has(sesion.usuario.id)) {
      return respuestas.noEncontrado('La cuenta no tiene ese proveedor vinculado.')
    }
    if (sesion.usuario.password === null) {
      return respuestas.codigo('ultimo_metodo_acceso', 'Google es tu única forma de entrar.')
    }
    db.vinculosGoogle.delete(sesion.usuario.id)
    return new HttpResponse(null, { status: 204 })
  }),
]
