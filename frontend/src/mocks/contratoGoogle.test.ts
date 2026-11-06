import { describe, expect, it } from 'vitest'
import { armarTokenDemo } from '@/features/auth/google/tokenDemo'
import { db } from '@/mocks/db'
import { iniciarSesionComo } from '@/test/render'

// La API simulada del inicio con Google debe responder como Host/WebApi (README › Inicio de sesión con Google).
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

const CONSENTIMIENTOS = { terminosVersion: 'v2.1', privacidadVersion: 'v1.4', marketing: true }
const entrar = (idToken: string, consentimientos?: unknown) =>
  llamar('/api/auth/google', { metodo: 'POST', body: { idToken, consentimientos } })
const vincular = (idToken: string, password: string, token?: string) =>
  llamar('/api/auth/google/vincular', { metodo: 'POST', token, body: { idToken, password } })

describe('contrato simulado: POST /api/auth/google', () => {
  it('un correo nuevo sin consentimientos responde 400 consentimiento_requerido y no crea nada', async () => {
    const r = await entrar(armarTokenDemo('nuevo@google.com'))

    expect(r.status).toBe(400)
    expect((await r.json()).codigo).toBe('consentimiento_requerido')
    expect(db.usuarios.some((u) => u.correo === 'nuevo@google.com')).toBe(false)
  })

  it('con versiones que no son las vigentes responde 400 consentimiento_no_valido', async () => {
    const r = await entrar(armarTokenDemo('nuevo@google.com'), {
      ...CONSENTIMIENTOS,
      terminosVersion: 'v0.1',
    })

    expect(r.status).toBe(400)
    expect((await r.json()).codigo).toBe('consentimiento_no_valido')
  })

  it('con los consentimientos crea la cuenta ACTIVA y SIN contraseña (201), con su vínculo y su sesión', async () => {
    const r = await entrar(armarTokenDemo('nuevo@google.com', 'Nueva Persona'), CONSENTIMIENTOS)

    expect(r.status).toBe(201)
    const cuerpo = await r.json()
    expect(cuerpo).toMatchObject({
      nombre: 'Nueva Persona',
      correo: 'nuevo@google.com',
      activo: true,
    })
    expect(cuerpo.token).toEqual(expect.any(String))
    const usuario = db.usuarios.find((u) => u.id === cuerpo.id)
    expect(usuario?.password).toBeNull()
    expect(db.vinculosGoogle.get(cuerpo.id)?.subject).toBe('sub-nuevo@google.com')
    expect(db.consentimientos.get(cuerpo.id)).toMatchObject({
      marketing: true,
      terminosVersion: 'v2.1',
    })
    expect(db.refresh?.usuarioId).toBe(cuerpo.id)
  })

  it('una identidad ya vinculada inicia sesión (200) sin pedir consentimientos', async () => {
    await entrar(armarTokenDemo('nuevo@google.com'), CONSENTIMIENTOS)
    db.refresh = null

    const r = await entrar(armarTokenDemo('nuevo@google.com'))

    expect(r.status).toBe(200)
    expect((await r.json()).token).toEqual(expect.any(String))
    expect(db.refresh).not.toBeNull()
  })

  it('un correo que ya tiene cuenta responde 409 vinculacion_requerida y NO abre sesión', async () => {
    const r = await entrar(armarTokenDemo('estandar@demo.com'), CONSENTIMIENTOS)

    expect(r.status).toBe(409)
    expect((await r.json()).codigo).toBe('vinculacion_requerida')
    expect(db.refresh).toBeNull()
    expect(db.vinculosGoogle.size).toBe(0)
  })

  it('un token falso, de otra forma o con el correo sin verificar es un 401 token_externo_invalido', async () => {
    for (const token of ['abc', 'mock-google|x', armarTokenDemo('nuevo@google.com', 'N', false)]) {
      const r = await entrar(token, CONSENTIMIENTOS)
      expect(r.status).toBe(401)
      expect((await r.json()).codigo).toBe('token_externo_invalido')
    }
    expect(db.usuarios.some((u) => u.correo === 'nuevo@google.com')).toBe(false)
  })

  it('sin idToken, o con uno de más de 4096 caracteres, es una solicitud inválida', async () => {
    expect((await llamar('/api/auth/google', { metodo: 'POST', body: {} })).status).toBe(400)
    expect((await entrar('x'.repeat(4097))).status).toBe(400)
  })

  it('una cuenta desactivada por un administrador no entra con Google', async () => {
    await entrar(armarTokenDemo('nuevo@google.com'), CONSENTIMIENTOS)
    const usuario = db.usuarios.find((u) => u.correo === 'nuevo@google.com')
    if (usuario) usuario.activo = false

    const r = await entrar(armarTokenDemo('nuevo@google.com'))

    expect((await r.json()).codigo).toBe('cuenta_desactivada')
  })

  it('una cuenta de Google no entra con contraseña (401 genérico) y no se bloquea', async () => {
    await entrar(armarTokenDemo('nuevo@google.com'), CONSENTIMIENTOS)
    for (let i = 0; i < 6; i++) {
      const r = await llamar('/api/auth/login', {
        metodo: 'POST',
        body: { email: 'nuevo@google.com', password: 'cualquier-clave-12345' },
      })
      expect(r.status).toBe(401)
      expect((await r.json()).codigo).toBeUndefined()
    }
    expect(db.usuarios.find((u) => u.correo === 'nuevo@google.com')?.bloqueadoHasta).toBeNull()
  })
})

describe('contrato simulado: POST /api/auth/google/vincular', () => {
  it('con la contraseña correcta vincula Google y abre la sesión', async () => {
    const r = await vincular(armarTokenDemo('estandar@demo.com'), 'Demo1234')

    expect(r.status).toBe(200)
    expect((await r.json()).token).toEqual(expect.any(String))
    const ana = db.usuarios.find((u) => u.correo === 'estandar@demo.com')
    expect(db.vinculosGoogle.get(ana?.id ?? '')?.subject).toBe('sub-estandar@demo.com')
  })

  it('con una contraseña incorrecta es un 401 y cuenta para el bloqueo', async () => {
    const r = await vincular(armarTokenDemo('estandar@demo.com'), 'incorrecta-12345')

    expect(r.status).toBe(401)
    expect(db.vinculosGoogle.size).toBe(0)
    expect(db.usuarios.find((u) => u.correo === 'estandar@demo.com')?.intentosFallidos).toBe(1)
  })

  it('a los 5 intentos fallidos la cuenta queda bloqueada (cuenta_bloqueada)', async () => {
    for (let i = 0; i < 4; i++)
      expect((await vincular(armarTokenDemo('otro@demo.com'), 'mala-mala-mala')).status).toBe(401)

    const quinto = await vincular(armarTokenDemo('otro@demo.com'), 'mala-mala-mala')

    expect((await quinto.json()).codigo).toBe('cuenta_bloqueada')
    const correcta = await vincular(armarTokenDemo('otro@demo.com'), 'Demo1234')
    expect((await correcta.json()).codigo).toBe('cuenta_bloqueada')
    expect(db.vinculosGoogle.size).toBe(0)
  })

  it('sin una cuenta con contraseña para ese correo responde el mismo 401 (no revela qué correos existen)', async () => {
    const r = await vincular(armarTokenDemo('nadie@demo.com'), 'Demo1234')

    expect(r.status).toBe(401)
    expect((await r.json()).codigo).toBeUndefined()
  })

  it('una cuenta que esperaba su código de activación queda activa (Google verificó el correo)', async () => {
    const r = await vincular(armarTokenDemo('inactiva@demo.com'), 'Demo1234')

    expect(r.status).toBe(200)
    expect(db.usuarios.find((u) => u.correo === 'inactiva@demo.com')?.activo).toBe(true)
  })

  it('una identidad de Google que ya es de otra cuenta no se puede vincular a esta', async () => {
    await entrar(armarTokenDemo('nuevo@google.com'), CONSENTIMIENTOS)
    const nueva = db.usuarios.find((u) => u.correo === 'nuevo@google.com')
    const otro = db.usuarios.find((u) => u.correo === 'otro@demo.com')
    // La identidad de «otro» (por correo) ya pertenece a la cuenta nueva.
    if (nueva && otro)
      db.vinculosGoogle.set(nueva.id, {
        subject: 'sub-otro@demo.com',
        vinculadoEn: new Date().toISOString(),
      })

    const r = await vincular(armarTokenDemo('otro@demo.com'), 'Demo1234')

    expect((await r.json()).codigo).toBe('proveedor_ya_vinculado')
  })

  it('un token falso es un 401 token_externo_invalido', async () => {
    const r = await vincular('abc', 'Demo1234')

    expect(r.status).toBe(401)
    expect((await r.json()).codigo).toBe('token_externo_invalido')
  })
})

describe('contrato simulado: vínculos de la cuenta', () => {
  it('lista si tiene contraseña y qué proveedores tiene vinculados', async () => {
    const token = iniciarSesionComo('estandar')
    expect(await (await llamar('/api/auth/vinculos', { token })).json()).toEqual({
      tienePassword: true,
      vinculos: [],
    })

    await vincular(armarTokenDemo('estandar@demo.com'), 'Demo1234')
    const cuerpo = await (
      await llamar('/api/auth/vinculos', { token: iniciarSesionComo('estandar') })
    ).json()

    expect(cuerpo.tienePassword).toBe(true)
    expect(cuerpo.vinculos).toEqual([
      { proveedor: 'google', vinculadoEn: expect.stringMatching(/Z$/) },
    ])
  })

  it('con contraseña se puede desvincular (204); otra vez, o un proveedor desconocido, es un 404', async () => {
    await vincular(armarTokenDemo('estandar@demo.com'), 'Demo1234')
    const token = iniciarSesionComo('estandar')

    expect((await llamar('/api/auth/vinculos/google', { metodo: 'DELETE', token })).status).toBe(
      204,
    )
    expect((await llamar('/api/auth/vinculos/google', { metodo: 'DELETE', token })).status).toBe(
      404,
    )
    expect((await llamar('/api/auth/vinculos/facebook', { metodo: 'DELETE', token })).status).toBe(
      404,
    )
  })

  it('sin contraseña NO se puede desvincular Google: es la única forma de entrar (ultimo_metodo_acceso)', async () => {
    const creada = await (await entrar(armarTokenDemo('nuevo@google.com'), CONSENTIMIENTOS)).json()

    const r = await llamar('/api/auth/vinculos/google', { metodo: 'DELETE', token: creada.token })

    expect(r.status).toBe(400)
    expect((await r.json()).codigo).toBe('ultimo_metodo_acceso')
    expect(db.vinculosGoogle.size).toBe(1)
    const vinculos = await (await llamar('/api/auth/vinculos', { token: creada.token })).json()
    expect(vinculos.tienePassword).toBe(false)
  })

  it('sin token responde 401', async () => {
    expect((await llamar('/api/auth/vinculos')).status).toBe(401)
    expect((await llamar('/api/auth/vinculos/google', { metodo: 'DELETE' })).status).toBe(401)
  })

  it('cambiar la contraseña de una cuenta sin contraseña responde sin_contrasena', async () => {
    const creada = await (await entrar(armarTokenDemo('nuevo@google.com'), CONSENTIMIENTOS)).json()

    const r = await llamar('/api/auth/cambiar-password', {
      metodo: 'POST',
      token: creada.token,
      body: { actual: 'lo-que-sea-12345', nueva: 'Otra-Clave-Segura-1' },
    })

    expect(r.status).toBe(400)
    expect((await r.json()).codigo).toBe('sin_contrasena')
  })
})
