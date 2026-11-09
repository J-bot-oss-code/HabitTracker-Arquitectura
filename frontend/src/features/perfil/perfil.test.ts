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

// Réplica de ProfileController (mocks/handlers/perfil.ts): el "servidor" valida de nuevo.
const url = (ruta: string) => `${window.location.origin}${ruta}`
const nivel1 = {
  perfilLaboral: 'OficinaDiurna',
  zonaHoraria: 'America/Santo_Domingo',
  avisoSaludAceptado: true,
  horaEntradaTrabajo: null,
  horaSalidaTrabajo: null,
  horaDespertar: null,
  horaDormir: null,
  edad: null,
  sexo: null,
  alturaCm: null,
  pesoKg: null,
  actividad: null,
  diasPorSemana: null,
  minutosPorSesion: null,
  metaSlug: 'primeros-5-km',
}
const consentimientos = {
  terminosVersion: 'v2.1',
  privacidadVersion: 'v1.4',
  datosSalud: true,
  marketing: false,
  compartirCumplimiento: false,
  estadisticasAnonimas: false,
}
const idLuis = () => db.usuarios.find((u) => u.correo === CORREOS.otro)?.id ?? ''

async function put(ruta: string, token: string, body: unknown) {
  return fetch(url(ruta), {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify(body),
  })
}

describe('contrato del perfil y la salud', () => {
  it('sin perfil responde 404 con ProblemDetails y código perfil_no_encontrado', async () => {
    const token = iniciarSesionComo('otro')
    db.perfiles.delete(idLuis())
    const r = await fetch(url('/api/profile'), { headers: { Authorization: `Bearer ${token}` } })
    expect(r.status).toBe(404)
    expect((await r.json()).codigo).toBe('perfil_no_encontrado')
  })

  it('el Administrador recibe 403 y sin sesión 401', async () => {
    const r = await fetch(url('/api/profile'), {
      headers: { Authorization: `Bearer ${iniciarSesionComo('admin')}` },
    })
    expect(r.status).toBe(403)
    expect((await fetch(url('/api/profile'))).status).toBe(401)
  })

  it('el nivel 1 se guarda sin consentimiento de salud; con peso o altura pide 409 (P1)', async () => {
    const token = iniciarSesionComo('otro')
    db.consentimientos.delete(idLuis())
    expect((await put('/api/profile', token, nivel1)).status).toBe(200)
    const conPeso = await put('/api/profile', token, { ...nivel1, pesoKg: 70 })
    expect(conPeso.status).toBe(409)
    expect((await conPeso.json()).codigo).toBe('consentimiento_salud_requerido')
  })

  it.each([
    ['edad', 15],
    ['edad', 30.5],
    ['pesoKg', 301],
    ['diasPorSemana', 8],
    ['perfilLaboral', 'Astronauta'],
    ['zonaHoraria', 'Zona/Inventada'],
    ['horaDespertar', '25:00'],
    ['avisoSaludAceptado', false],
    ['sexo', 'mujer'],
  ])('rechaza %s = %s con 400', async (campo, valor) => {
    const token = iniciarSesionComo('otro')
    expect((await put('/api/profile', token, { ...nivel1, [campo]: valor })).status).toBe(400)
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

  it('el plan de interés empieza al elegirlo y se conserva si no cambia', async () => {
    const token = iniciarSesionComo('otro')
    const primero = await (await put('/api/profile', token, nivel1)).json()
    expect(primero.inicioPlan).toEqual(expect.stringMatching(/Z$/))
    const segundo = await (
      await put('/api/profile', token, { ...nivel1, perfilLaboral: 'Remoto' })
    ).json()
    expect(segundo.inicioPlan).toBe(primero.inicioPlan)
  })

  it('un bloque del cuestionario sin consentimiento es 409; con él se guarda y se evalúa', async () => {
    const token = iniciarSesionComo('otro')
    db.consentimientos.delete(idLuis())
    const bloque = { condiciones: ['Embarazo'], alergias: ['Ninguna'] }
    expect((await put('/api/profile/health/condiciones', token, bloque)).status).toBe(409)

    await put('/api/privacy/consents', token, consentimientos)
    const r = await put('/api/profile/health/condiciones', token, bloque)
    expect(r.status).toBe(200)
    const dto = await r.json()
    expect(dto.evaluacion.restricciones).toContain('SinRestriccionCalorica')
    expect(dto.evaluacion.completitud).toEqual({
      objetivos: false,
      seguridad: false,
      condiciones: true,
    })
  })

  it.each([
    [
      '/api/profile/health/condiciones',
      { condiciones: ['Ninguna', 'Asma'], alergias: ['Ninguna'] },
    ],
    [
      '/api/profile/health/objetivos',
      {
        objetivos: ['DormirMejor', 'Hidratarme', 'MasEnergia', 'ReducirEstres'],
        zonas: [],
        experiencia: 'Ninguna',
        lugar: 'Gimnasio',
      },
    ],
    [
      '/api/profile/health/objetivos',
      { objetivos: ['DormirMejor'], zonas: ['Espalda'], experiencia: 'Ninguna', lugar: 'Gimnasio' },
    ],
    ['/api/profile/health/seguridad', { dolorDePecho: true }],
  ])('rechaza un bloque inválido en %s con 400', async (ruta, cuerpo) => {
    const token = iniciarSesionComo('estandar')
    expect((await put(ruta, token, cuerpo)).status).toBe(400)
  })

  it('con el consentimiento retirado las respuestas se ven pero la evaluación no las usa', async () => {
    const token = iniciarSesionComo('estandar')
    const seguridad = {
      afeccionCardiacaOPresionAlta: true,
      dolorDePecho: false,
      mareosOPerdidaDeConocimiento: false,
      otraEnfermedadCronica: false,
      medicacionCronica: false,
      problemaOseoOArticular: false,
      soloActividadSupervisada: false,
    }
    const evaluacion = (await (await put('/api/profile/health/seguridad', token, seguridad)).json())
      .evaluacion
    expect(evaluacion.nivel).toBe('ConsultarMedico')
    // Mismo orden que el backend (valor del enum), no alfabético.
    expect(evaluacion.restricciones).toEqual(['SoloIntensidadBaja', 'SinIntensidadAlta'])
    await put('/api/privacy/consents', token, { ...consentimientos, datosSalud: false })

    const dto = await (
      await fetch(url('/api/profile/health'), { headers: { Authorization: `Bearer ${token}` } })
    ).json()
    expect(dto.seguridad.afeccionCardiacaOPresionAlta).toBe(true)
    expect(dto.usaDatosDeSalud).toBe(false)
    expect(dto.evaluacion.nivel).toBe('Normal')
  })
})
