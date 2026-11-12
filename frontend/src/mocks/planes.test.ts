import { existsSync, readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import {
  PREGUNTAS_SEGURIDAD,
  type Alergia,
  type CondicionSalud,
  type Objetivo,
} from '@/features/perfil/types'
import type { PlanDetalleDto } from '@/features/planes/types'
import { db, type CuestionarioMock } from '@/mocks/db'
import { CORREOS, iniciarSesionComo } from '@/test/render'
import { PLANES_MOCK } from './handlers/planesData'

// La API simulada de los planes debe responder como PlansController. Estas pruebas fijan lo que la interfaz da
// por supuesto; si el backend cambia, se actualizan aquí primero.
const url = (ruta: string) => `${window.location.origin}${ruta}`

async function llamar(ruta: string, token?: string) {
  const headers: Record<string, string> = {}
  if (token) headers.Authorization = `Bearer ${token}`
  return fetch(url(ruta), { headers })
}

const idDe = (quien: keyof typeof CORREOS) =>
  db.usuarios.find((u) => u.correo === CORREOS[quien])!.id

interface Recomendados {
  recomendados: { plan: { slug: string }; motivos: string[]; advertencias: string[] }[]
  noAptos: { plan: { slug: string }; razones: { motivo: string; mensaje: string }[] }[]
  personalizadoConSalud: boolean
}

async function recomendados(quien: keyof typeof CORREOS): Promise<Recomendados> {
  const respuesta = await llamar('/api/plans/recommended', iniciarSesionComo(quien))
  expect(respuesta.status).toBe(200)
  return respuesta.json()
}

const slugs = (r: Recomendados) => r.recomendados.map((x) => x.plan.slug)

// Un cuestionario completo (objetivos, seguridad y condiciones) para el usuario, con consentimiento.
function responderCuestionario(
  quien: keyof typeof CORREOS,
  {
    objetivos = ['ReducirEstres'],
    condiciones = ['Ninguna'],
    alergias = ['Ninguna'],
    seguridadAfirmativa = false,
  }: {
    objetivos?: Objetivo[]
    condiciones?: CondicionSalud[]
    alergias?: Alergia[]
    seguridadAfirmativa?: boolean
  } = {},
) {
  const id = idDe(quien)
  const ahora = new Date().toISOString()
  db.consentimientos.set(id, { ...db.consentimientos.get(id)!, datosSalud: true })
  db.cuestionarios.set(id, {
    objetivos: { objetivos, zonas: [], experiencia: 'Principiante', lugar: 'CasaSinEquipo' },
    objetivosRespondidoEn: ahora,
    seguridad: Object.fromEntries(
      PREGUNTAS_SEGURIDAD.map((k) => [k, seguridadAfirmativa && k === 'dolorDePecho']),
    ) as NonNullable<CuestionarioMock['seguridad']>,
    seguridadRespondidoEn: ahora,
    condiciones: { condiciones, alergias },
    condicionesRespondidoEn: ahora,
  })
}

// --- La copia del catálogo frente al JSON del backend ---

const RUTA_JSON = resolve(
  process.cwd(),
  '..',
  'Modules/HabitTracker/HabitTracker.Infrastructure/Contenido/catalogo-planes.json',
)

interface PlanJson {
  slug: string
  nombre: string
  resumen: string
  descripcion: string
  area: string
  nivel: string
  intensidad: string
  semanas: number
  requiereCuestionario: boolean
  avisoSeguridad: string
  beneficios: string[]
  sexo?: string
  edadMinima?: number
  edadMaxima?: number
  perfilesLaborales?: string[]
  lugar?: string
  impacto?: boolean
  zonasCorporales?: string[]
  contraindicaciones?: string[]
  alergenos?: string[]
  guiaSemanal?: { semana: number; texto: string }[]
  tareas: Record<string, unknown>[]
}

// Sin el repositorio completo (por ejemplo, una imagen que solo copia frontend/) esta comparación no se puede
// hacer; en el CI y en local el JSON siempre está.
describe.skipIf(!existsSync(RUTA_JSON))(
  'copia del catálogo y catalogo-planes.json del backend',
  () => {
    // Dentro del describe pero guardado: skipIf no evita que este cuerpo se ejecute al recolectar las pruebas.
    const json: { planes: PlanJson[] } = existsSync(RUTA_JSON)
      ? JSON.parse(readFileSync(RUTA_JSON, 'utf8'))
      : { planes: [] }

    it('tiene los mismos planes', () => {
      expect(PLANES_MOCK.map((p) => p.slug).sort()).toEqual(json.planes.map((p) => p.slug).sort())
    })

    it('cada plan coincide en todo lo que el servidor devuelve', () => {
      for (const real of json.planes) {
        const copia = PLANES_MOCK.find((p) => p.slug === real.slug) as PlanDetalleDto
        const esperado = {
          nombre: real.nombre,
          resumen: real.resumen,
          descripcion: real.descripcion,
          area: real.area,
          nivel: real.nivel,
          intensidad: real.intensidad,
          semanas: real.semanas,
          requiereCuestionario: real.requiereCuestionario,
          avisoSeguridad: real.avisoSeguridad,
          beneficios: real.beneficios,
          sexo: real.sexo ?? 'Todos',
          edadMinima: real.edadMinima ?? 16,
          edadMaxima: real.edadMaxima ?? 100,
          lugar: real.lugar ?? null,
          impacto: real.impacto ?? false,
          contraindicaciones: real.contraindicaciones ?? [],
          alergenos: real.alergenos ?? [],
          tareas: real.tareas.map((t) => ({
            orden: t.orden,
            nombre: t.nombre,
            instrucciones: t.instrucciones,
            periodicidad: t.periodicidad,
            cantidad: t.cantidad,
            unidad: t.unidad,
            versionMinima: t.versionMinima,
            anclaSugerida: t.anclaSugerida,
          })),
          guiaSemanal: (real.guiaSemanal ?? []).map((g) => ({ semana: g.semana, texto: g.texto })),
        }
        const { slug: _slug, perfilesLaborales: jornadas, zonasCorporales: zonas, ...resto } = copia
        void _slug
        expect(resto, real.slug).toEqual(esperado)
        expect([...jornadas].sort(), real.slug).toEqual([...(real.perfilesLaborales ?? [])].sort())
        expect([...zonas].sort(), real.slug).toEqual([...(real.zonasCorporales ?? [])].sort())
      }
    })

    it('los 13 planes de lanzamiento: 7 Básicos y 6 Regulares', () => {
      expect(PLANES_MOCK).toHaveLength(13)
      expect(PLANES_MOCK.filter((p) => p.nivel === 'Basico')).toHaveLength(7)
      expect(PLANES_MOCK.filter((p) => p.nivel === 'Regular')).toHaveLength(6)
    })
  },
)

describe('GET /api/plans y /api/plans/{slug} (anónimos)', () => {
  it('el catálogo es público: 13 planes, Básicos primero y por nombre, sin datos internos', async () => {
    const respuesta = await llamar('/api/plans')
    const lista: { slug: string; nombre: string; nivel: string }[] = await respuesta.json()

    expect(respuesta.status).toBe(200)
    expect(lista).toHaveLength(13)
    const niveles = lista.map((p) => p.nivel)
    expect(niveles).toEqual(
      [...niveles].sort((a, b) => (a === 'Basico' ? -1 : b === 'Basico' ? 1 : 0)),
    )
    for (const nivel of ['Basico', 'Regular']) {
      const nombres = lista.filter((p) => p.nivel === nivel).map((p) => p.nombre)
      expect(nombres).toEqual([...nombres].sort((a, b) => (a < b ? -1 : a > b ? 1 : 0)))
    }
    expect(JSON.stringify(lista)).not.toMatch(/"id"|"activo"|tipExtra|tareas/)
  })

  it('filtra por área, nivel y jornada; un plan sin jornadas es para todas', async () => {
    const descanso: { slug: string }[] = await (await llamar('/api/plans?area=Descanso')).json()
    expect(descanso.map((p) => p.slug).sort()).toEqual([
      'desconexion-digital-nocturna',
      'dormir-mejor',
      'mi-dia-turno-nocturno',
    ])

    const regulares: unknown[] = await (await llamar('/api/plans?nivel=Regular')).json()
    expect(regulares).toHaveLength(6)

    const fisico: { slug: string }[] = await (
      await llamar('/api/plans?perfilLaboral=TrabajoFisico')
    ).json()
    const fisicos = fisico.map((p) => p.slug)
    expect(fisicos).toContain('mi-dia-trabajo-fisico')
    expect(fisicos).toContain('hidratacion-con-calor')
    expect(fisicos).not.toContain('mi-dia-oficina')
  })

  it('un valor fuera del enum es 400', async () => {
    for (const consulta of ['area=Magia', 'nivel=Premium', 'perfilLaboral=Astronauta']) {
      expect((await llamar(`/api/plans?${consulta}`)).status).toBe(400)
    }
  })

  it('el detalle trae tareas con su versión mínima y la guía, sin el tip extra', async () => {
    const respuesta = await llamar('/api/plans/primeros-5-km')
    const plan: PlanDetalleDto = await respuesta.json()

    expect(respuesta.status).toBe(200)
    expect(plan.guiaSemanal).toHaveLength(8)
    expect(plan.tareas.every((t) => t.versionMinima.length > 0)).toBe(true)
    expect(JSON.stringify(plan)).not.toContain('tipExtra')
  })

  it('un plan que no existe es 404 con ProblemDetails y código (no un 404 vacío)', async () => {
    const respuesta = await llamar('/api/plans/no-existe')
    const cuerpo = await respuesta.json()

    expect(respuesta.status).toBe(404)
    expect(respuesta.headers.get('content-type')).toContain('application/problem+json')
    expect(cuerpo.codigo).toBe('plan_no_encontrado')
  })
})

describe('GET /api/plans/recommended', () => {
  it('exige sesión de Estándar', async () => {
    expect((await llamar('/api/plans/recommended')).status).toBe(401)
    expect((await llamar('/api/plans/recommended', iniciarSesionComo('admin'))).status).toBe(403)
  })

  it('antes del onboarding es 404 perfil_no_encontrado', async () => {
    db.perfiles.delete(idDe('estandar'))
    const respuesta = await llamar('/api/plans/recommended', iniciarSesionComo('estandar'))

    expect(respuesta.status).toBe(404)
    expect((await respuesta.json()).codigo).toBe('perfil_no_encontrado')
  })

  it.each([
    ['estandar', 'mi-dia-oficina'],
    ['otro', 'mi-dia-turno-nocturno'],
  ] as const)(
    'sin cuestionario, "Mi día" de la jornada de %s va primero',
    async (quien, esperado) => {
      const resultado = await recomendados(quien)

      expect(slugs(resultado)[0]).toBe(esperado)
      expect(resultado.personalizadoConSalud).toBe(false)
      // Las variantes de otras jornadas no se recomiendan.
      expect(slugs(resultado).filter((s) => s.startsWith('mi-dia-'))).toEqual([esperado])
      expect(resultado.noAptos).toEqual([])
    },
  )

  it('sin cuestionario, lo que no pide preguntas va antes y lo demás avisa que las hará', async () => {
    const resultado = await recomendados('estandar')
    const lista = slugs(resultado)

    expect(lista.indexOf('respiracion-anti-estres')).toBeLessThan(
      lista.indexOf('perder-grasa-sostenible'),
    )
    const peso = resultado.recomendados.find((r) => r.plan.slug === 'perder-grasa-sostenible')!
    expect(peso.advertencias).toContain('Antes de empezarlo te haremos unas preguntas de salud.')
  })

  it('con el cuestionario afina por objetivos y deja "Mi día" primero', async () => {
    responderCuestionario('estandar', { objetivos: ['DormirMejor'] })
    const resultado = await recomendados('estandar')

    expect(resultado.personalizadoConSalud).toBe(true)
    expect(slugs(resultado)[0]).toBe('mi-dia-oficina')
    expect(slugs(resultado).slice(0, 4)).toContain('dormir-mejor')
    const dormir = resultado.recomendados.find((r) => r.plan.slug === 'dormir-mejor')!
    expect(dormir.motivos).toContain('Va con tu objetivo: dormir mejor.')
  })

  it('con embarazo, el plan de peso es "no apto" y se muestra con su razón', async () => {
    responderCuestionario('estandar', { condiciones: ['Embarazo'] })
    const resultado = await recomendados('estandar')

    expect(slugs(resultado)).not.toContain('perder-grasa-sostenible')
    const noApto = resultado.noAptos.find((n) => n.plan.slug === 'perder-grasa-sostenible')!
    expect(noApto.razones.map((r) => r.motivo)).toEqual(['SinPlanesDePeso'])
    expect(noApto.razones[0].mensaje).not.toMatch(/embarazo/i)
  })

  it('con un "sí" en la revisión de seguridad solo se recomienda intensidad baja y se avisa del médico', async () => {
    responderCuestionario('estandar', { seguridadAfirmativa: true })
    const resultado = await recomendados('estandar')

    const intensidades = new Map(PLANES_MOCK.map((p) => [p.slug, p.intensidad]))
    for (const s of slugs(resultado)) expect(intensidades.get(s), s).toBe('Baja')
    expect(resultado.noAptos.map((n) => n.plan.slug)).toEqual(
      expect.arrayContaining(['primeros-5-km', 'ganar-masa-muscular']),
    )
    expect(resultado.recomendados[0].advertencias).toContain(
      'Antes de empezar, consulta a tu médico.',
    )
  })

  it('con hipertensión, los planes con agua avisan de consultar al médico', async () => {
    responderCuestionario('estandar', { condiciones: ['Hipertension'] })
    const resultado = await recomendados('estandar')

    const miDia = resultado.recomendados.find((r) => r.plan.slug === 'mi-dia-oficina')!
    expect(miDia.advertencias).toContain(
      'Consulta a tu médico cuánta agua te conviene tomar al día.',
    )
    const respirar = resultado.recomendados.find((r) => r.plan.slug === 'respiracion-anti-estres')!
    expect(respirar.advertencias).not.toContain(
      'Consulta a tu médico cuánta agua te conviene tomar al día.',
    )
  })

  it('indicar una alergia no excluye planes mientras el catálogo no tenga alérgenos', async () => {
    // Ningún plan de lanzamiento declara alérgenos; la regla de los alérgenos se prueba en el backend.
    responderCuestionario('estandar', { alergias: ['Mani'] })
    const resultado = await recomendados('estandar')

    expect(resultado.noAptos).toEqual([])
  })

  it('sin el consentimiento de salud el cuestionario no se usa, pero la edad sí protege', async () => {
    responderCuestionario('estandar', { condiciones: ['Embarazo', 'Hipertension'] })
    db.consentimientos.set(idDe('estandar'), {
      ...db.consentimientos.get(idDe('estandar'))!,
      datosSalud: false,
    })
    const sinConsentimiento = await recomendados('estandar')
    expect(sinConsentimiento.personalizadoConSalud).toBe(false)
    expect(sinConsentimiento.noAptos).toEqual([])
    // Tampoco se avisa por una condición que la persona dio y ya no permite usar.
    const miDia = sinConsentimiento.recomendados.find((r) => r.plan.slug === 'mi-dia-oficina')!
    expect(miDia.advertencias).not.toContain(
      'Consulta a tu médico cuánta agua te conviene tomar al día.',
    )

    db.perfiles.set(idDe('estandar'), { ...db.perfiles.get(idDe('estandar'))!, edad: 17 })
    const menor = await recomendados('estandar')
    expect(menor.noAptos.map((n) => n.plan.slug)).toContain('perder-grasa-sostenible')
  })

  it('un plan para otra edad o sexo es "no apto"', async () => {
    db.perfiles.set(idDe('estandar'), { ...db.perfiles.get(idDe('estandar'))!, edad: 17 })
    const resultado = await recomendados('estandar')

    const masa = resultado.noAptos.find((n) => n.plan.slug === 'ganar-masa-muscular')!
    expect(masa.razones.map((r) => r.motivo)).toContain('FueraDeRangoDeEdad')
  })
})
