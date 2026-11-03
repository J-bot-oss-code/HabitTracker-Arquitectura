import { afterEach, describe, expect, it } from 'vitest'
import { db } from '@/mocks/db'
import { CORREOS, iniciarSesionComo } from '@/test/render'
import {
  aceptoVersionesVigentes,
  borrarRegistroPendiente,
  guardarRegistroPendiente,
  leerRegistroPendiente,
  purgarRegistroPendienteCaducado,
} from './registroPendiente'

const CLAVE = 'habittracker.registro-pendiente'
const base = {
  correo: 'Nueva@Demo.com',
  terminosVersion: 'v2.1',
  privacidadVersion: 'v1.4',
  marketing: true,
  metaSlug: 'dormir-mejor',
  datosCorporales: null,
}

afterEach(() => localStorage.clear())

describe('registro pendiente en el dispositivo', () => {
  it('solo lo lee la cuenta que lo creó (sin distinguir mayúsculas)', () => {
    guardarRegistroPendiente(base)
    expect(leerRegistroPendiente('nueva@demo.com')).toMatchObject({ metaSlug: 'dormir-mejor' })
    expect(leerRegistroPendiente('otra@demo.com')).toBeNull()
  })

  it('caduca a los 7 días', () => {
    guardarRegistroPendiente(base)
    const ocho = Date.now() + 8 * 24 * 60 * 60 * 1000
    expect(leerRegistroPendiente('nueva@demo.com', ocho)).toBeNull()
  })

  it('descarta una meta o unos datos corporales manipulados', () => {
    localStorage.setItem(
      CLAVE,
      JSON.stringify({
        ...base,
        correo: 'nueva@demo.com',
        metaSlug: '<script>',
        datosCorporales: { edad: 3, sexo: 'x' },
        guardadoEn: Date.now(),
      }),
    )
    expect(leerRegistroPendiente('nueva@demo.com')).toMatchObject({
      metaSlug: null,
      datosCorporales: null,
    })
  })

  it('ignora contenido que no tiene la forma esperada', () => {
    localStorage.setItem(CLAVE, '{"correo": 1}')
    expect(leerRegistroPendiente('nueva@demo.com')).toBeNull()
    localStorage.setItem(CLAVE, 'no es json')
    expect(leerRegistroPendiente('nueva@demo.com')).toBeNull()
  })

  it('lo caducado se borra del navegador al leerlo y al arrancar (privacidad)', () => {
    guardarRegistroPendiente(base)
    const ocho = Date.now() + 8 * 24 * 60 * 60 * 1000
    purgarRegistroPendienteCaducado(ocho)
    expect(localStorage.getItem(CLAVE)).toBeNull()

    guardarRegistroPendiente(base)
    leerRegistroPendiente('nueva@demo.com', ocho)
    expect(localStorage.getItem(CLAVE)).toBeNull()
  })

  it('un registro fechado en el futuro no vale y se borra', () => {
    localStorage.setItem(
      CLAVE,
      JSON.stringify({ ...base, correo: 'nueva@demo.com', guardadoEn: Date.now() + 60_000_000 }),
    )
    expect(leerRegistroPendiente('nueva@demo.com')).toBeNull()
    expect(localStorage.getItem(CLAVE)).toBeNull()
  })

  it('los datos corporales caducan antes (48 h) que el resto del registro', () => {
    guardarRegistroPendiente({
      ...base,
      datosCorporales: { edad: 30, sexo: 'mujer', alturaCm: 165, pesoKg: 60, actividad: 'ligera' },
    })
    const tresDias = Date.now() + 3 * 24 * 60 * 60 * 1000
    expect(leerRegistroPendiente('nueva@demo.com', tresDias)).toMatchObject({
      metaSlug: 'dormir-mejor',
      datosCorporales: null,
    })
  })

  it('solo se aceptan las versiones legales vigentes', () => {
    guardarRegistroPendiente({ ...base, terminosVersion: 'v2.0' })
    const leido = leerRegistroPendiente('nueva@demo.com')
    expect(leido && aceptoVersionesVigentes(leido)).toBe(false)
  })

  it('no borra el registro pendiente de otra cuenta', () => {
    guardarRegistroPendiente(base)
    expect(leerRegistroPendiente('otra@demo.com')).toBeNull()
    expect(localStorage.getItem(CLAVE)).not.toBeNull()
  })

  it('se borra al terminar', () => {
    guardarRegistroPendiente(base)
    borrarRegistroPendiente()
    expect(localStorage.getItem(CLAVE)).toBeNull()
  })
})

// Contrato propuesto (mocks/handlers/propuestas/perfil.ts): el "servidor" valida de nuevo.
const url = (ruta: string) => `${window.location.origin}${ruta}`
const perfilValido = {
  edad: 30,
  sexo: null,
  alturaCm: 170,
  pesoKg: 70,
  actividad: 'moderada',
  diasPorSemana: 3,
  minutosPorSesion: 30,
  metaSlug: 'primeros-5-km',
  avisoSaludAceptado: true,
}
const consentimientos = {
  terminosVersion: 'v2.1',
  privacidadVersion: 'v1.4',
  datosSalud: true,
  marketing: false,
  compartirCumplimiento: false,
  estadisticasAnonimas: false,
}

async function put(ruta: string, token: string, body: unknown) {
  return fetch(url(ruta), {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(body),
  })
}

describe('contrato propuesto del perfil', () => {
  it('sin perfil responde 404 con ProblemDetails (no es "pieza no disponible")', async () => {
    const token = iniciarSesionComo('otro')
    db.perfiles.delete(db.usuarios.find((u) => u.correo === CORREOS.otro)?.id ?? '')
    const r = await fetch(url('/api/profile'), { headers: { Authorization: `Bearer ${token}` } })
    expect(r.status).toBe(404)
    expect((await r.json()).title).toBe('No encontrado.')
  })

  it('el Administrador recibe 403 y sin sesión 401', async () => {
    const r = await fetch(url('/api/profile'), {
      headers: { Authorization: `Bearer ${iniciarSesionComo('admin')}` },
    })
    expect(r.status).toBe(403)
    expect((await fetch(url('/api/profile'))).status).toBe(401)
  })

  it('no guarda datos de salud sin consentimiento (409)', async () => {
    const token = iniciarSesionComo('otro')
    db.consentimientos.delete(db.usuarios.find((u) => u.correo === CORREOS.otro)?.id ?? '')
    expect((await put('/api/profile', token, perfilValido)).status).toBe(409)
  })

  it.each([
    ['edad', 15],
    ['edad', 30.5],
    ['pesoKg', 301],
    ['diasPorSemana', 8],
    ['metaSlug', 'inventada'],
    ['avisoSaludAceptado', false],
    ['sexo', 'otro'],
  ])('rechaza %s = %s con 400', async (campo, valor) => {
    const token = iniciarSesionComo('otro')
    expect((await put('/api/profile', token, { ...perfilValido, [campo]: valor })).status).toBe(400)
  })

  it('solo acepta las versiones vigentes de términos y privacidad', async () => {
    const token = iniciarSesionComo('otro')
    const r = await put('/api/privacy/consents', token, {
      ...consentimientos,
      terminosVersion: 'v1.0',
    })
    expect(r.status).toBe(400)
    const ok = await put('/api/privacy/consents', token, consentimientos)
    expect(ok.status).toBe(200)
    expect((await ok.json()).aceptadoEn).toEqual(expect.stringMatching(/Z$/))
  })

  it('el plan empieza al elegir meta y se conserva si la meta no cambia', async () => {
    const token = iniciarSesionComo('otro')
    const primero = await (await put('/api/profile', token, perfilValido)).json()
    expect(primero.inicioPlan).toEqual(expect.stringMatching(/Z$/))
    const segundo = await (await put('/api/profile', token, { ...perfilValido, pesoKg: 69 })).json()
    expect(segundo.inicioPlan).toBe(primero.inicioPlan)
  })
})
