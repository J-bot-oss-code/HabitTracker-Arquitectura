import { describe, expect, it } from 'vitest'
import { db, TOKEN_ACTIVACION_DEMO } from '@/mocks/db'
import { iniciarSesionComo } from '@/test/render'

// La API simulada de Control de Acceso debe responder como Host/WebApi (README › Control de Acceso).
// Estas pruebas fijan las reglas del contrato que la interfaz da por supuestas.
const url = (ruta: string) => `${window.location.origin}${ruta}`

async function llamar(
  ruta: string,
  opciones: {
    metodo?: string
    token?: string
    body?: unknown
    cabeceras?: Record<string, string>
  } = {},
) {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...opciones.cabeceras,
  }
  if (opciones.token) headers.Authorization = `Bearer ${opciones.token}`
  return fetch(url(ruta), {
    method: opciones.metodo ?? 'GET',
    headers,
    body: opciones.body === undefined ? undefined : JSON.stringify(opciones.body),
  })
}

const CONSENTIMIENTOS = { terminosVersion: 'v2.1', privacidadVersion: 'v1.4', marketing: false }
const CLAVE = 'Clave-Segura-2026'
const DOCE_CARACTERES = 'Clave1234567'
const registro = (cambios: Record<string, unknown> = {}) => ({
  nombre: 'Persona Nueva',
  correo: 'nueva@demo.com',
  password: CLAVE,
  consentimientos: CONSENTIMIENTOS,
  ...cambios,
})

describe('contrato simulado: registro', () => {
  it('sin los consentimientos vigentes no se crea la cuenta (consentimiento_no_valido)', async () => {
    for (const consentimientos of [
      undefined,
      { ...CONSENTIMIENTOS, terminosVersion: 'v1.0' },
      { ...CONSENTIMIENTOS, privacidadVersion: 'v0.9' },
      { terminosVersion: 'v2.1', privacidadVersion: 'v1.4' },
    ]) {
      const r = await llamar('/api/auth/register', {
        metodo: 'POST',
        body: registro({ consentimientos }),
      })
      expect(r.status).toBe(400)
      expect((await r.json()).codigo).toBe('consentimiento_no_valido')
    }
    expect(db.usuarios.some((u) => u.correo === 'nueva@demo.com')).toBe(false)
  })

  it('con ellos crea la cuenta y guarda el consentimiento junto con el usuario', async () => {
    const r = await llamar('/api/auth/register', { metodo: 'POST', body: registro() })

    expect(r.status).toBe(201)
    const cuerpo = await r.json()
    expect(cuerpo).toMatchObject({ activo: false, token: null, requiereAceptarDocumentos: false })
    expect(db.consentimientos.get(cuerpo.id)).toMatchObject({
      terminosVersion: 'v2.1',
      privacidadVersion: 'v1.4',
      marketing: false,
    })
  })

  it.each(['corta1', 'Clave123456', 'SoloLetrasLargasSinNumero', '123456789012345'])(
    'rechaza la contraseña %s (12 a 1024 caracteres con letra y número)',
    async (password) => {
      const r = await llamar('/api/auth/register', { metodo: 'POST', body: registro({ password }) })
      expect(r.status).toBe(400)
      expect((await r.json()).codigo).toBe('contrasena_no_valida')
    },
  )

  it('acepta una contraseña de exactamente 12 caracteres con letras y números', async () => {
    const r = await llamar('/api/auth/register', {
      metodo: 'POST',
      body: registro({ password: DOCE_CARACTERES }),
    })
    expect(r.status).toBe(201)
  })

  it('un correo repetido se rechaza con correo_ya_registrado', async () => {
    const r = await llamar('/api/auth/register', {
      metodo: 'POST',
      body: registro({ correo: 'estandar@demo.com' }),
    })
    expect(r.status).toBe(400)
    expect((await r.json()).codigo).toBe('correo_ya_registrado')
  })
})

describe('contrato simulado: activación con correo y código de 6 dígitos', () => {
  it('activa con el correo y el código', async () => {
    const r = await llamar('/api/auth/activate', {
      metodo: 'POST',
      body: { correo: 'inactiva@demo.com', codigo: TOKEN_ACTIVACION_DEMO },
    })
    expect(r.status).toBe(200)
    expect(db.usuarios.find((u) => u.correo === 'inactiva@demo.com')?.activo).toBe(true)
  })

  it('el código solo no basta: el cuerpo antiguo {token} es una solicitud inválida', async () => {
    const r = await llamar('/api/auth/activate', {
      metodo: 'POST',
      body: { token: TOKEN_ACTIVACION_DEMO },
    })
    expect(r.status).toBe(400)
  })

  it('un código mal formado se rechaza sin consultar la cuenta', async () => {
    for (const codigo of ['12345', '1234567', 'abcdef', '12 456']) {
      const r = await llamar('/api/auth/activate', {
        metodo: 'POST',
        body: { correo: 'inactiva@demo.com', codigo },
      })
      expect(r.status).toBe(400)
    }
  })

  it('un código incorrecto, una cuenta ya activa o un correo inexistente dan el mismo rechazo', async () => {
    for (const body of [
      { correo: 'inactiva@demo.com', codigo: '000000' },
      { correo: 'estandar@demo.com', codigo: TOKEN_ACTIVACION_DEMO },
      { correo: 'nadie@demo.com', codigo: TOKEN_ACTIVACION_DEMO },
    ]) {
      const r = await llamar('/api/auth/activate', { metodo: 'POST', body })
      expect(r.status).toBe(400)
      expect((await r.json()).codigo).toBe('codigo_invalido')
    }
  })
})

describe('contrato simulado: inicio de sesión y códigos de error', () => {
  const entrar = (email: string, password = 'Demo1234') =>
    llamar('/api/auth/login', { metodo: 'POST', body: { email, password } })

  it('devuelve el token y los avisos de consentimiento y de baja', async () => {
    const cuerpo = await (await entrar('estandar@demo.com')).json()
    expect(cuerpo).toMatchObject({
      activo: true,
      requiereAceptarDocumentos: false,
      bajaProgramadaPara: null,
    })
    expect(cuerpo.token).toEqual(expect.any(String))
  })

  it('una cuenta que nunca aceptó los documentos recibe requiereAceptarDocumentos=true', async () => {
    const cuerpo = await (await entrar('admin@demo.com')).json()
    expect(cuerpo.requiereAceptarDocumentos).toBe(true)
  })

  it('con la clave correcta dice por qué no se puede entrar (cuenta_no_activa)', async () => {
    const r = await entrar('inactiva@demo.com')
    expect(r.status).toBe(400)
    expect((await r.json()).codigo).toBe('cuenta_no_activa')
  })

  it('con la clave incorrecta no revela nada: 401 genérico', async () => {
    const r = await entrar('inactiva@demo.com', 'incorrecta-12345')
    expect(r.status).toBe(401)
    expect((await r.json()).codigo).toBeUndefined()
  })

  it('a los 5 intentos la cuenta queda bloqueada (cuenta_bloqueada)', async () => {
    for (let i = 0; i < 4; i++)
      expect((await entrar('otro@demo.com', 'mala-mala-mala')).status).toBe(401)
    const quinto = await entrar('otro@demo.com', 'mala-mala-mala')
    expect(quinto.status).toBe(400)
    expect((await quinto.json()).codigo).toBe('cuenta_bloqueada')
    const correcta = await entrar('otro@demo.com')
    expect((await correcta.json()).codigo).toBe('cuenta_bloqueada')
  })

  it('una cuenta desactivada por un administrador responde cuenta_desactivada', async () => {
    const otro = db.usuarios.find((u) => u.correo === 'otro@demo.com')
    if (otro) otro.activo = false
    const r = await entrar('otro@demo.com')
    expect((await r.json()).codigo).toBe('cuenta_desactivada')
  })
})

describe('contrato simulado: refresh token rotativo (POST /api/auth/refresh)', () => {
  const CSRF = { 'X-HabitTracker-Csrf': '1' }
  const refrescar = (cabeceras: Record<string, string> = CSRF) =>
    llamar('/api/auth/refresh', { metodo: 'POST', cabeceras })

  async function iniciar(email = 'estandar@demo.com') {
    return (
      await llamar('/api/auth/login', { metodo: 'POST', body: { email, password: 'Demo1234' } })
    ).json()
  }

  it('sin la cabecera anti-CSRF responde 403 csrf_requerido y no gasta la sesión', async () => {
    await iniciar()
    const r = await refrescar({})
    expect(r.status).toBe(403)
    expect((await r.json()).codigo).toBe('csrf_requerido')
    expect(db.refresh).not.toBeNull()
  })

  it('sin sesión (sin cookie) responde 401 sesion_invalida', async () => {
    const r = await refrescar()
    expect(r.status).toBe(401)
    expect((await r.json()).codigo).toBe('sesion_invalida')
  })

  it('con la cookie entrega un token de acceso nuevo y rota el refresh token', async () => {
    const primero = await iniciar()
    const cookieAntes = db.refresh?.valor

    const r = await refrescar()

    expect(r.status).toBe(200)
    const cuerpo = await r.json()
    expect(cuerpo.token).toEqual(expect.any(String))
    expect(cuerpo.id).toBe(primero.id)
    expect(db.refresh?.valor).not.toBe(cookieAntes)
  })

  it('el logout revoca el refresh token', async () => {
    const { token } = await iniciar()
    expect((await llamar('/api/auth/logout', { metodo: 'POST', token })).status).toBe(200)

    expect((await refrescar()).status).toBe(401)
  })

  it('cambiar la contraseña revoca el refresh token', async () => {
    const { token } = await iniciar()
    const cambio = await llamar('/api/auth/cambiar-password', {
      metodo: 'POST',
      token,
      body: { actual: 'Demo1234', nueva: 'Otra-Clave-Segura-1' },
    })
    expect(cambio.status).toBe(200)

    expect((await refrescar()).status).toBe(401)
  })

  it('una cuenta desactivada ya no puede renovar', async () => {
    await iniciar()
    const ana = db.usuarios.find((u) => u.correo === 'estandar@demo.com')
    if (ana) ana.activo = false

    expect((await refrescar()).status).toBe(401)
  })
})

describe('contrato simulado: cambiar la contraseña', () => {
  it('una contraseña actual incorrecta es un 400 clave_actual_incorrecta (nunca 401: cerraría la sesión)', async () => {
    const r = await llamar('/api/auth/cambiar-password', {
      metodo: 'POST',
      token: iniciarSesionComo('estandar'),
      body: { actual: 'no-es-esta-12345', nueva: 'Otra-Clave-Segura-1' },
    })
    expect(r.status).toBe(400)
    expect((await r.json()).codigo).toBe('clave_actual_incorrecta')
  })

  it('la nueva debe cumplir la política de 12 caracteres (contrasena_no_valida)', async () => {
    const r = await llamar('/api/auth/cambiar-password', {
      metodo: 'POST',
      token: iniciarSesionComo('estandar'),
      body: { actual: 'Demo1234', nueva: 'Corta123' },
    })
    expect(r.status).toBe(400)
    expect((await r.json()).codigo).toBe('contrasena_no_valida')
  })
})

describe('contrato simulado: GET /api/auth/me', () => {
  it('trae el nombre y el aviso de re-aceptación', async () => {
    const r = await llamar('/api/auth/me', { token: iniciarSesionComo('estandar') })
    expect(await r.json()).toMatchObject({
      nombre: 'Ana Estándar',
      correo: 'estandar@demo.com',
      requiereAceptarDocumentos: false,
    })
  })
})
